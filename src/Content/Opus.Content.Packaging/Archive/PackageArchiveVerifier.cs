using System.Globalization;
using Opus.Content.Packaging.Diagnostics;
using Opus.Content.Packaging.Manifest;
using Opus.Content.Packaging.Paths;
using Opus.Content.Packaging.Signing;
using Opus.Content.Packaging.Validation;

namespace Opus.Content.Packaging.Archive;

/// <summary>
/// Verifies a <c>.opkg</c> archive end to end: container structure and bounds (through the
/// reader), integrity (each manifest-declared file re-hashed against the manifest), and — when a
/// public key is supplied — authenticity (the manifest signature). Reuses the existing
/// <c>OPKG-FILE-*</c> integrity codes so a tester sees the same diagnostics whether validating a
/// directory or an archive.
/// </summary>
public static class PackageArchiveVerifier
{
    /// <summary>Verifies the archive described by <paramref name="request"/>.</summary>
    public static PackageArchiveVerificationResult Verify(PackageArchiveVerifyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ArchivePath);
        request.Limits.Validate();
        if (request.RequireSignature && request.PublicKey is null)
        {
            throw new ArgumentException(
                "RequireSignature needs a PublicKey to verify against.", nameof(request));
        }

        var diagnostics = new List<PackageDiagnostic>();
        if (!OpusPackageArchiveReader.TryOpen(
                request.ArchivePath, request.Limits, out var reader, out var openDiagnostics))
        {
            diagnostics.AddRange(openDiagnostics);
            return BuildResult(false, null, diagnostics);
        }

        using (reader)
        {
            return VerifyOpened(request, reader!, openDiagnostics);
        }
    }

    /// <summary>
    /// Verifies an already-open archive. The caller keeps the reader alive so a trusted
    /// extraction can consume the exact same file handle without a path-reopen race.
    /// </summary>
    public static PackageArchiveVerificationResult VerifyOpened(
        PackageArchiveVerifyRequest request,
        OpusPackageArchiveReader reader,
        IReadOnlyList<PackageDiagnostic>? initialDiagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(reader);
        if (request.RequireSignature && request.PublicKey is null)
        {
            throw new ArgumentException(
                "RequireSignature needs a PublicKey to verify against.",
                nameof(request));
        }

        var diagnostics = initialDiagnostics is null
            ? new List<PackageDiagnostic>()
            : new List<PackageDiagnostic>(initialDiagnostics);
        if (!reader.TryReadManifestBytes(out var manifestBytes, out var manifestError))
        {
            if (manifestError is not null)
            {
                diagnostics.Add(manifestError);
            }

            return BuildResult(false, null, diagnostics);
        }

        var manifest = ParseManifest(manifestBytes, diagnostics);
        if (manifest is null)
        {
            return BuildResult(false, null, diagnostics);
        }

        VerifyIntegrity(reader, manifest, diagnostics);
        var signatureVerified = VerifySignature(reader, request, manifestBytes, diagnostics);
        return BuildResult(signatureVerified, manifest, diagnostics);
    }

    private static ContentPackageManifest? ParseManifest(byte[] manifestBytes, List<PackageDiagnostic> diagnostics)
    {
        using var stream = new MemoryStream(manifestBytes, writable: false);
        var result = ContentPackageManifestReader.Read(stream);
        if (result.IsOk)
        {
            return result.Unwrap();
        }

        diagnostics.Add(Error(
            PackageDiagnosticCode.ManifestMalformed,
            PackageDiagnosticTarget.Manifest,
            result.UnwrapErr().Message,
            "Fix the manifest JSON syntax and schema.",
            "package.manifest.malformed"));
        return null;
    }

    private static void VerifyIntegrity(
        OpusPackageArchiveReader reader, ContentPackageManifest manifest, List<PackageDiagnostic> diagnostics)
    {
        var declared = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in manifest.Files)
        {
            if (!PackageRelativePath.TryCreate(file.Path, out var path, out var reason))
            {
                diagnostics.Add(Error(
                    PackageDiagnosticCode.PathInvalid,
                    PackageDiagnosticTarget.Manifest,
                    $"Manifest path '{file.Path}' is invalid: {reason}",
                    "Use a normalised package-relative path without traversal.",
                    "package.path.invalid",
                    ("path", file.Path), ("reason", reason)));
                continue;
            }

            if (!declared.Add(path.Value))
            {
                diagnostics.Add(Error(
                    PackageDiagnosticCode.PathDuplicate,
                    PackageDiagnosticTarget.File(path),
                    $"Manifest declares '{path.Value}' more than once.",
                    "Remove duplicate file entries.",
                    "package.path.duplicate",
                    ("path", path.Value)));
                continue;
            }

            VerifyDeclaredFile(reader, file, path, diagnostics);
        }

        VerifyUnlisted(reader, declared, diagnostics);
    }

    private static void VerifyDeclaredFile(
        OpusPackageArchiveReader reader,
        ContentPackageFile file,
        PackageRelativePath path,
        List<PackageDiagnostic> diagnostics)
    {
        if (!reader.ContainsEntry(path.Value))
        {
            diagnostics.Add(Error(
                PackageDiagnosticCode.FileMissing,
                PackageDiagnosticTarget.File(path),
                $"Declared file '{path.Value}' is not present in the archive.",
                "Re-pack the package so the manifest and archive entries agree.",
                "package.file.missing",
                ("path", path.Value)));
            return;
        }

        var hash = reader.HashEntry(path.Value);
        if (hash.ExceededLimit)
        {
            diagnostics.Add(Error(
                PackageDiagnosticCode.ArchiveEntryTooLarge,
                PackageDiagnosticTarget.File(path),
                $"Archive entry '{path.Value}' expanded past the per-entry budget while hashing.",
                "The entry's header understates its size; re-pack from trusted content.",
                "package.archive.entryTooLarge",
                ("entry", path.Value)));
            return;
        }

        if (hash.ByteCount != file.SizeBytes)
        {
            diagnostics.Add(Error(
                PackageDiagnosticCode.FileSizeMismatch,
                PackageDiagnosticTarget.File(path),
                $"Manifest size for '{path.Value}' is {file.SizeBytes} bytes, archive entry is {hash.ByteCount} bytes.",
                "Regenerate the manifest after changing package files.",
                "package.file.sizeMismatch",
                ("path", path.Value),
                ("declared", file.SizeBytes.ToString(CultureInfo.InvariantCulture)),
                ("actual", hash.ByteCount.ToString(CultureInfo.InvariantCulture))));
        }

        if (!string.Equals(hash.Sha256Hex, file.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(Error(
                PackageDiagnosticCode.FileHashMismatch,
                PackageDiagnosticTarget.File(path),
                $"SHA-256 mismatch for '{path.Value}'.",
                "Regenerate the manifest after changing package files.",
                "package.file.hashMismatch",
                ("path", path.Value),
                ("declared", file.Sha256),
                ("actual", hash.Sha256Hex)));
        }
    }

    private static void VerifyUnlisted(
        OpusPackageArchiveReader reader, HashSet<string> declared, List<PackageDiagnostic> diagnostics)
    {
        foreach (var entryName in reader.PayloadEntryNames)
        {
            if (!declared.Contains(entryName))
            {
                // An archive entry the manifest does not list is not covered by the signed
                // manifest (the Merkle root), so it is untrusted content smuggled into the
                // container. Unlike the directory validator's dev-convenience warning, archive
                // verification treats it as an error.
                diagnostics.Add(Error(
                    PackageDiagnosticCode.FileUnlisted,
                    PackageDiagnosticTarget.File(entryName),
                    $"Archive entry '{entryName}' exists but is not listed in the signed manifest.",
                    "Re-pack so every archive entry is covered by the manifest.",
                    "package.file.unlisted",
                    ("path", entryName)));
            }
        }
    }

    private static bool VerifySignature(
        OpusPackageArchiveReader reader,
        PackageArchiveVerifyRequest request,
        byte[] manifestBytes,
        List<PackageDiagnostic> diagnostics)
    {
        if (!reader.HasSignature)
        {
            diagnostics.Add(request.RequireSignature
                ? Error(
                    PackageDiagnosticCode.SignatureMissing,
                    PackageDiagnosticTarget.Package,
                    "Archive is unsigned but a signature is required.",
                    "Sign the package, or verify without the signature requirement.",
                    "package.signature.missing")
                : Warning(
                    PackageDiagnosticCode.PackageUnsigned,
                    PackageDiagnosticTarget.Package,
                    "Archive is unsigned; only integrity was verified, not authenticity.",
                    "Sign the package to allow authenticity verification.",
                    "package.unsigned"));
            return false;
        }

        if (!reader.TryReadSignatureBytes(out var signatureBytes, out var sigReadError))
        {
            if (sigReadError is not null)
            {
                diagnostics.Add(sigReadError);
            }

            return false;
        }

        var signature = ParseSignature(signatureBytes, diagnostics);
        if (signature is null)
        {
            return false;
        }

        if (request.PublicKey is null)
        {
            diagnostics.Add(Warning(
                PackageDiagnosticCode.SignaturePresentNotVerified,
                PackageDiagnosticTarget.Package,
                "Archive is signed but no public key was supplied; authenticity was not checked.",
                "Pass the trusted public key to verify authenticity.",
                "package.signature.notVerified"));
            return false;
        }

        return EvaluateSignature(
            PackageSignatureVerifier.Verify(manifestBytes, signature, request.PublicKey), diagnostics);
    }

    private static PackageSignature? ParseSignature(byte[] signatureBytes, List<PackageDiagnostic> diagnostics)
    {
        using var stream = new MemoryStream(signatureBytes, writable: false);
        var result = PackageSignatureReader.Read(stream);
        if (result.IsOk)
        {
            return result.Unwrap();
        }

        diagnostics.Add(Error(
            PackageDiagnosticCode.SignatureMalformed,
            PackageDiagnosticTarget.Package,
            result.UnwrapErr().Message,
            "Re-sign the package; the signature envelope is malformed.",
            "package.signature.malformed"));
        return null;
    }

    private static bool EvaluateSignature(
        PackageSignatureVerification verification, List<PackageDiagnostic> diagnostics)
    {
        switch (verification)
        {
            case PackageSignatureVerification.Valid:
                return true;
            case PackageSignatureVerification.AlgorithmUnsupported:
                diagnostics.Add(Error(
                    PackageDiagnosticCode.SignatureAlgorithmUnsupported,
                    PackageDiagnosticTarget.Package,
                    "Signature names an algorithm this verifier does not implement.",
                    "Re-sign the package with a supported algorithm.",
                    "package.signature.algorithmUnsupported"));
                return false;
            case PackageSignatureVerification.ManifestHashMismatch:
                diagnostics.Add(Error(
                    PackageDiagnosticCode.SignatureManifestHashMismatch,
                    PackageDiagnosticTarget.Manifest,
                    "Manifest digest in the signature does not match the manifest in the archive.",
                    "The manifest was altered after signing; re-pack and re-sign.",
                    "package.signature.manifestHashMismatch"));
                return false;
            case PackageSignatureVerification.Malformed:
                diagnostics.Add(Error(
                    PackageDiagnosticCode.SignatureMalformed,
                    PackageDiagnosticTarget.Package,
                    "Signature envelope is missing required fields or its signature is not valid base64.",
                    "Re-sign the package; the signature envelope is malformed.",
                    "package.signature.malformed"));
                return false;
            case PackageSignatureVerification.SignatureInvalid:
            default:
                diagnostics.Add(Error(
                    PackageDiagnosticCode.SignatureInvalid,
                    PackageDiagnosticTarget.Package,
                    "Signature did not verify against the supplied public key.",
                    "The package was not signed by that key or was tampered with.",
                    "package.signature.invalid"));
                return false;
        }
    }

    private static PackageArchiveVerificationResult BuildResult(
        bool signatureVerified, ContentPackageManifest? manifest, List<PackageDiagnostic> diagnostics)
    {
        var sorted = PackageValidationResult.From(manifest, diagnostics);
        return new PackageArchiveVerificationResult(
            sorted.IsValid, signatureVerified, manifest, sorted.Diagnostics);
    }

    private static PackageDiagnostic Error(
        PackageDiagnosticCode code,
        PackageDiagnosticTarget target,
        string message,
        string remediation,
        string messageKey,
        params (string Key, string Value)[] arguments) =>
        PackageDiagnostic.Create(
            PackageDiagnosticSeverity.Error, code, target, message, remediation, messageKey,
            PackageDiagnosticArguments.Create(arguments));

    private static PackageDiagnostic Warning(
        PackageDiagnosticCode code,
        PackageDiagnosticTarget target,
        string message,
        string remediation,
        string messageKey,
        params (string Key, string Value)[] arguments) =>
        PackageDiagnostic.Create(
            PackageDiagnosticSeverity.Warning, code, target, message, remediation, messageKey,
            PackageDiagnosticArguments.Create(arguments));
}
