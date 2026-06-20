using System.Globalization;
using System.Text;
using Opus.Content.Packaging.Diagnostics;
using Opus.Content.Packaging.Manifest;
using Opus.Content.Packaging.Paths;
using Opus.Content.Packaging.Signing;
using Opus.Content.Packaging.Validation;

namespace Opus.Content.Packaging.Archive;

/// <summary>
/// Packs a content directory into a signed-or-unsigned <c>.opkg</c> archive. It validates the
/// manifest paths and the size budget, serialises the exact manifest bytes (signing them when a
/// key is supplied), and writes the archive atomically (temp file then rename) so a crash never
/// leaves a half-written <c>.opkg</c> in place. The serialised manifest bytes that are signed are
/// the same bytes stored in the archive, so the on-disk manifest always matches the signature.
/// </summary>
public static class PackageArchivePacker
{
    /// <summary>Packs the request's content into a <c>.opkg</c> archive.</summary>
    public static PackageArchivePackResult Pack(PackageArchivePackRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ContentRoot);
        ArgumentNullException.ThrowIfNull(request.Manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputArchivePath);
        request.Limits.Validate();

        var diagnostics = new List<PackageDiagnostic>();
        var contentRoot = Path.GetFullPath(request.ContentRoot);
        if (!Directory.Exists(contentRoot))
        {
            diagnostics.Add(PackError(
                PackageDiagnosticCode.PackageRootMissing,
                $"Content root '{contentRoot}' does not exist.",
                "Pass a valid content directory to pack.",
                "package.root.missing",
                ("path", contentRoot)));
            return new PackageArchivePackResult(false, null, false, diagnostics);
        }

        if (!ValidateManifestForPacking(request.Manifest, request.Limits, diagnostics))
        {
            return new PackageArchivePackResult(false, null, false, diagnostics);
        }

        var manifestBytes = Encoding.UTF8.GetBytes(ContentPackageManifestReader.Write(request.Manifest));
        var signatureBytes = BuildSignature(request, manifestBytes, out var signed);
        if (!TryWriteArchiveAtomically(contentRoot, request, manifestBytes, signatureBytes, diagnostics))
        {
            return new PackageArchivePackResult(false, null, signed, diagnostics);
        }

        return new PackageArchivePackResult(
            true, Path.GetFullPath(request.OutputArchivePath), signed, diagnostics);
    }

    private static byte[]? BuildSignature(
        PackageArchivePackRequest request, byte[] manifestBytes, out bool signed)
    {
        signed = false;
        if (request.SigningKey is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(request.SigningKeyId))
        {
            throw new ArgumentException(
                "A signing key id is required when a signing key is supplied.", nameof(request));
        }

        var signature = PackageSigner.Sign(manifestBytes, request.SigningKey, request.SigningKeyId);
        signed = true;
        return Encoding.UTF8.GetBytes(PackageSignatureReader.Write(signature));
    }

    private static bool ValidateManifestForPacking(
        ContentPackageManifest manifest, OpusPackageArchiveLimits limits, List<PackageDiagnostic> diagnostics)
    {
        if (manifest.Files.Count > limits.MaxEntryCount)
        {
            diagnostics.Add(PackError(
                PackageDiagnosticCode.ArchiveEntryCountExceeded,
                $"Manifest declares {manifest.Files.Count} files, above the {limits.MaxEntryCount} archive entry limit.",
                "Reduce the number of files or raise the archive entry-count limit.",
                "package.archive.entryCountExceeded",
                ("count", manifest.Files.Count.ToString(CultureInfo.InvariantCulture)),
                ("limit", limits.MaxEntryCount.ToString(CultureInfo.InvariantCulture))));
            return false;
        }

        long total = 0;
        var totalExceeded = false;
        var hadError = false;
        var declaredPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.Files)
        {
            if (!PackageRelativePath.TryCreate(file.Path, out var relativePath, out var reason))
            {
                diagnostics.Add(PackError(
                    PackageDiagnosticCode.PathInvalid,
                    $"Manifest path '{file.Path}' is invalid: {reason}",
                    "Use a normalised package-relative path without traversal.",
                    "package.path.invalid",
                    ("path", file.Path), ("reason", reason)));
                hadError = true;
                continue;
            }

            if (!declaredPaths.Add(relativePath.Value))
            {
                diagnostics.Add(PackError(
                    PackageDiagnosticCode.PathDuplicate,
                    $"Manifest path '{file.Path}' is declared more than once.",
                    "Keep one manifest entry for each case-insensitive package path.",
                    "package.path.duplicate",
                    ("path", file.Path)));
                hadError = true;
            }

            if (file.SizeBytes < 0)
            {
                diagnostics.Add(PackError(
                    PackageDiagnosticCode.FileSizeMismatch,
                    $"File '{file.Path}' declares a negative size.",
                    "Regenerate the manifest from the current content tree.",
                    "package.file.sizeInvalid",
                    ("entry", file.Path)));
                hadError = true;
            }
            else if (file.SizeBytes > limits.MaxEntryUncompressedBytes)
            {
                diagnostics.Add(PackError(
                    PackageDiagnosticCode.ArchiveEntryTooLarge,
                    $"File '{file.Path}' is {file.SizeBytes} bytes, above the {limits.MaxEntryUncompressedBytes}-byte per-entry limit.",
                    "Split the asset or raise the per-entry limit.",
                    "package.archive.entryTooLarge",
                    ("entry", file.Path)));
                hadError = true;
            }

            if (!IsSha256(file.Sha256))
            {
                diagnostics.Add(PackError(
                    PackageDiagnosticCode.FileHashMismatch,
                    $"File '{file.Path}' does not declare a valid SHA-256 digest.",
                    "Regenerate the manifest from the current content tree.",
                    "package.file.hashInvalid",
                    ("entry", file.Path)));
                hadError = true;
            }

            if (file.SizeBytes >= 0)
            {
                if (file.SizeBytes > limits.MaxTotalUncompressedBytes - total)
                {
                    totalExceeded = true;
                }
                else
                {
                    total += file.SizeBytes;
                }
            }
        }

        if (totalExceeded)
        {
            diagnostics.Add(PackError(
                PackageDiagnosticCode.ArchiveTooLarge,
                $"Package content exceeds the {limits.MaxTotalUncompressedBytes}-byte total limit.",
                "Reduce package content or raise the total limit.",
                "package.archive.tooLarge",
                ("limit", limits.MaxTotalUncompressedBytes.ToString(CultureInfo.InvariantCulture))));
            hadError = true;
        }

        return !hadError;
    }

    private static bool IsSha256(string? value)
    {
        if (value is null || value.Length != 64)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryWriteArchiveAtomically(
        string contentRoot,
        PackageArchivePackRequest request,
        byte[] manifestBytes,
        byte[]? signatureBytes,
        List<PackageDiagnostic> diagnostics)
    {
        var outputPath = Path.GetFullPath(request.OutputArchivePath);
        var directory = Path.GetDirectoryName(outputPath) ?? Directory.GetCurrentDirectory();
        string? tempPath = null;
        try
        {
            Directory.CreateDirectory(directory);
            using (var tempStream = CreateTempFile(directory, out tempPath))
            {
                OpusPackageArchiveWriter.Write(
                    contentRoot, request.Manifest, manifestBytes, signatureBytes, tempStream);
            }

            File.Move(tempPath!, outputPath, overwrite: true);
            return true;
        }
        catch (IOException ex)
        {
            return FailWrite(tempPath, outputPath, ex.Message, diagnostics);
        }
        catch (InvalidDataException ex)
        {
            return FailWrite(tempPath, outputPath, ex.Message, diagnostics);
        }
        catch (UnauthorizedAccessException ex)
        {
            return FailWrite(tempPath, outputPath, ex.Message, diagnostics);
        }
    }

    private static FileStream CreateTempFile(string directory, out string tempPath)
    {
        const int maxAttempts = 16;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            tempPath = Path.Combine(directory, Path.GetRandomFileName());
            try
            {
                return new FileStream(
                    tempPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None);
            }
            catch (IOException) when (File.Exists(tempPath))
            {
            }
        }

        tempPath = string.Empty;
        throw new IOException("Unable to allocate a unique archive staging file.");
    }

    private static bool FailWrite(
        string? tempPath, string outputPath, string detail, List<PackageDiagnostic> diagnostics)
    {
        TryDeleteTemp(tempPath);
        diagnostics.Add(PackError(
            PackageDiagnosticCode.ArchiveWriteFailed,
            $"Failed to write archive '{outputPath}': {detail}",
            "Check the output path is writable and not locked by another process.",
            "package.archive.writeFailed",
            ("path", outputPath)));
        return false;
    }

    private static void TryDeleteTemp(string? tempPath)
    {
        if (string.IsNullOrEmpty(tempPath))
        {
            return;
        }

        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup; a leftover temp file must not mask the original write failure.
        }
        catch (UnauthorizedAccessException)
        {
            // As above.
        }
    }

    private static PackageDiagnostic PackError(
        PackageDiagnosticCode code,
        string message,
        string remediation,
        string messageKey,
        params (string Key, string Value)[] arguments) =>
        PackageDiagnostic.Create(
            PackageDiagnosticSeverity.Error,
            code,
            PackageDiagnosticTarget.Package,
            message,
            remediation,
            messageKey,
            PackageDiagnosticArguments.Create(arguments));
}
