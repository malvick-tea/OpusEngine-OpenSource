using System;
using System.Collections.Generic;
using System.IO;
using Opus.Content.Packaging.Diagnostics;
using Opus.Content.Packaging.Manifest;
using Opus.Content.Packaging.Validation;
using Opus.Foundation;

namespace Opus.Content.Packaging.Generation;

/// <summary>
/// Produces a <see cref="ContentPackageManifest"/> from a content directory: it walks the
/// tree the same way the validator does (recursive, skipping reparse points and the manifest
/// file itself), infers each file's asset type from its extension, and computes the size and
/// streaming SHA-256 the validator later re-checks. It is the inverse of
/// <see cref="PackageValidator"/> — a manifest generated here validates clean against the
/// same Opus identity, so authoring a package is a repeatable pipeline instead of
/// hand-editing JSON.
/// </summary>
/// <remarks>
/// The generator computes the manifest but does not persist it: the caller chooses where and
/// how to write (so an atomic write or a chosen path stays the caller's decision). Archive /
/// signing container policy remains the open M6 lead decision and is intentionally out of
/// scope here.
/// </remarks>
public sealed class PackageManifestGenerator
{
    private static readonly ManifestFormatVersion CurrentFormatVersion =
        new(PackageValidator.SupportedManifestMajor, PackageValidator.SupportedManifestMinor);

    /// <summary>Generates a manifest for the content under <see cref="PackageGenerationRequest.PackageRoot"/>.</summary>
    public PackageGenerationResult Generate(PackageGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.PackageRoot);
        ArgumentNullException.ThrowIfNull(request.Package);

        var diagnostics = new List<PackageDiagnostic>();
        var packageRoot = Path.GetFullPath(request.PackageRoot);
        if (!Directory.Exists(packageRoot))
        {
            diagnostics.Add(PackageDiagnostic.Create(
                PackageDiagnosticSeverity.Error,
                PackageDiagnosticCode.PackageRootMissing,
                PackageDiagnosticTarget.Package,
                $"Package root '{packageRoot}' does not exist.",
                "Pass a valid content directory to generate a manifest from.",
                "package.root.missing",
                PackageDiagnosticArguments.Create(("path", packageRoot))));
            return new PackageGenerationResult(null, diagnostics);
        }

        var files = EnumerateContentFiles(packageRoot, request, diagnostics);
        var manifest = new ContentPackageManifest(
            CurrentFormatVersion,
            request.Package,
            BuildEngineTarget(),
            request.Authoring,
            request.Entrypoints,
            request.RequiredFeatures,
            files);
        return new PackageGenerationResult(manifest, diagnostics);
    }

    private static List<ContentPackageFile> EnumerateContentFiles(
        string packageRoot,
        PackageGenerationRequest request,
        List<PackageDiagnostic> diagnostics)
    {
        // Mirror the validator's enumeration so every file the generator omits and every file
        // it lists agree with the validator's unlisted-file pass: recurse, skip reparse points
        // (symlinks the validator also refuses to follow), and skip the manifest itself.
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = false,
        };

        var entries = new List<ContentPackageFile>();
        foreach (var physicalPath in Directory.EnumerateFiles(packageRoot, "*", options))
        {
            var relative = Path.GetRelativePath(packageRoot, physicalPath).Replace('\\', '/');
            if (string.Equals(relative, request.ManifestFileName, StringComparison.Ordinal)
                || string.Equals(relative, PackageValidator.DefaultSignatureFileName, StringComparison.Ordinal))
            {
                continue;
            }

            if (!TryResolveType(relative, request, out var assetType))
            {
                diagnostics.Add(BuildUnknownTypeWarning(relative));
                continue;
            }

            var info = new FileInfo(physicalPath);
            entries.Add(new ContentPackageFile(
                relative,
                assetType,
                info.Length,
                PackageFileHash.ComputeSha256HexFile(physicalPath),
                null));
        }

        // Deterministic order so a regenerated manifest is a stable diff regardless of the
        // platform's directory enumeration order.
        entries.Sort(static (left, right) => string.CompareOrdinal(left.Path, right.Path));
        return entries;
    }

    private static bool TryResolveType(string relativePath, PackageGenerationRequest request, out string assetType)
    {
        var extension = Path.GetExtension(relativePath).ToLowerInvariant();
        if (request.TypeOverrides.TryGetValue(extension, out var overrideType)
            && !string.IsNullOrWhiteSpace(overrideType))
        {
            assetType = overrideType;
            return true;
        }

        return PackageAssetTypeInference.TryInferType(relativePath, out assetType);
    }

    private static PackageDiagnostic BuildUnknownTypeWarning(string relativePath)
    {
        var extension = Path.GetExtension(relativePath);
        return PackageDiagnostic.Create(
            PackageDiagnosticSeverity.Warning,
            PackageDiagnosticCode.GeneratedFileTypeUnknown,
            PackageDiagnosticTarget.File(relativePath),
            $"Could not infer a package asset type for '{relativePath}' (extension '{extension}'); it was omitted from the manifest.",
            "Rename the file to a recognised extension or supply a type override for this extension.",
            "package.generate.typeUnknown",
            PackageDiagnosticArguments.Create(("path", relativePath), ("extension", extension)));
    }

    private static ContentPackageTarget BuildEngineTarget()
    {
        var identity = EngineIdentity.Current;
        var version = identity.ProductVersion.ToString();
        return new ContentPackageTarget(
            identity.ProductName,
            version,
            version,
            identity.AssemblyNamePrefix,
            Array.Empty<string>());
    }
}
