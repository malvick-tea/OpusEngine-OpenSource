using System.Globalization;
using Opus.Content.Packaging.Diagnostics;
using Opus.Content.Packaging.Manifest;
using Opus.Content.Packaging.Paths;
using Opus.Foundation;

namespace Opus.Content.Packaging.Validation;

/// <summary>
/// Headless content package validator. It validates file IO, manifest shape, hashes,
/// and supported source asset formats without referencing renderer or D3D12 assemblies.
/// </summary>
/// <remarks>
/// Lead decisions intentionally left outside this type: archive/signing policy, full
/// offline bake formats, and whether unlisted files become hard errors in alpha CI.
/// </remarks>
public sealed class PackageValidator
{
    /// <summary>Default package manifest file name.</summary>
    public const string DefaultManifestFileName = "opus.package.json";

    /// <summary>Reserved package signature file name. Like the manifest, it is container
    /// metadata rather than a payload file, so it is never reported as an unlisted file and is
    /// never emitted into a generated manifest's file list.</summary>
    public const string DefaultSignatureFileName = "opus.package.sig";

    /// <summary>Manifest schema major version this validator understands.</summary>
    public const int SupportedManifestMajor = 1;

    /// <summary>Manifest schema minor version this validator natively knows. Newer minor
    /// versions still load — the validator emits a warning and ignores unknown additive
    /// fields preserved by <c>JsonExtensionData</c>.</summary>
    public const int SupportedManifestMinor = 0;

    /// <summary>Default in-memory budget (128 MiB) for deep, content-aware validation of a
    /// single declared file. Integrity (size + SHA-256) is always streamed and never bounded
    /// by this; a file above the budget is integrity-checked and then reported with
    /// <see cref="Diagnostics.PackageDiagnosticCode.FileTooLargeForDeepValidation"/> instead of
    /// being read into memory, so a manifest declaring a very large file cannot drive the
    /// validator out of memory on an unconditional whole-file read.</summary>
    public const long DefaultMaxDeepValidationBytes = 128L * 1024 * 1024;

    private readonly DeclaredFileValidator _fileValidator = new();

    /// <summary>
    /// Validates a package directory and returns all diagnostics discovered in one pass.
    /// </summary>
    public PackageValidationResult ValidateDirectory(PackageValidationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.PackageRoot);
        if (request.MaxDeepValidationBytes is < 1 or > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.MaxDeepValidationBytes,
                $"MaxDeepValidationBytes must be between 1 and {int.MaxValue}.");
        }

        var diagnostics = new List<PackageDiagnostic>();
        var packageRoot = Path.GetFullPath(request.PackageRoot);
        if (!Directory.Exists(packageRoot))
        {
            diagnostics.Add(PackageDiagnosticBuilder.Error(
                PackageDiagnosticCode.PackageRootMissing,
                PackageDiagnosticTarget.Package,
                $"Package root '{packageRoot}' does not exist.",
                "Pass a valid package directory path.",
                "package.root.missing",
                PackageDiagnosticArguments.Create(("path", packageRoot))));
            return PackageValidationResult.From(null, diagnostics);
        }

        var manifestPath = Path.Combine(packageRoot, request.ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            diagnostics.Add(PackageDiagnosticBuilder.Error(
                PackageDiagnosticCode.ManifestMissing,
                PackageDiagnosticTarget.Manifest,
                $"Package manifest '{request.ManifestFileName}' was not found.",
                "Create opus.package.json at the package root.",
                "package.manifest.missing",
                PackageDiagnosticArguments.Create(("manifest", request.ManifestFileName))));
            return PackageValidationResult.From(null, diagnostics);
        }

        var manifest = ReadManifest(manifestPath, diagnostics);
        if (manifest is null)
        {
            return PackageValidationResult.From(null, diagnostics);
        }

        ValidateManifestHeader(manifest, diagnostics);
        ValidateFiles(packageRoot, manifest, request, diagnostics);
        return PackageValidationResult.From(manifest, diagnostics);
    }

    private static ContentPackageManifest? ReadManifest(string manifestPath, List<PackageDiagnostic> diagnostics)
    {
        using var stream = File.OpenRead(manifestPath);
        var result = ContentPackageManifestReader.Read(stream);
        if (result.IsOk)
        {
            return result.Unwrap();
        }

        var error = result.UnwrapErr();
        diagnostics.Add(PackageDiagnosticBuilder.Error(
            PackageDiagnosticCode.ManifestMalformed,
            PackageDiagnosticTarget.Manifest,
            error.Message,
            "Fix the manifest JSON syntax and schema.",
            "package.manifest.malformed"));
        return null;
    }

    private static void ValidateManifestHeader(ContentPackageManifest manifest, List<PackageDiagnostic> diagnostics)
    {
        if (manifest.FormatVersion is null || manifest.Engine is null || manifest.Package is null)
        {
            diagnostics.Add(PackageDiagnosticBuilder.Error(
                PackageDiagnosticCode.ManifestIdentityInvalid,
                PackageDiagnosticTarget.Manifest,
                "Package manifest is missing required root sections.",
                "Declare formatVersion, package, engine, and files sections.",
                "package.manifest.requiredSectionsMissing"));
            return;
        }

        ValidateManifestFormatVersion(manifest.FormatVersion, diagnostics);

        if (!string.Equals(manifest.Engine.Product, EngineIdentity.Current.ProductName, StringComparison.Ordinal))
        {
            diagnostics.Add(PackageDiagnosticBuilder.Error(
                PackageDiagnosticCode.ManifestIdentityInvalid,
                PackageDiagnosticTarget.Manifest,
                $"Package targets '{manifest.Engine.Product}' instead of '{EngineIdentity.Current.ProductName}'.",
                "Set engine.product to Opus for OpusEngine packages.",
                "package.manifest.productInvalid",
                PackageDiagnosticArguments.Create(("product", manifest.Engine.Product))));
        }

        ValidateVersion(manifest.Engine.TargetVersion, diagnostics);
        ValidateRequiredFeatures(manifest.RequiredFeatures, diagnostics);
    }

    private static void ValidateManifestFormatVersion(
        ManifestFormatVersion version,
        List<PackageDiagnostic> diagnostics)
    {
        if (version.Major != SupportedManifestMajor)
        {
            diagnostics.Add(PackageDiagnosticBuilder.Error(
                PackageDiagnosticCode.ManifestVersionUnsupported,
                PackageDiagnosticTarget.Manifest,
                $"Manifest major version {version.Major} is not supported.",
                $"Use manifest major version {SupportedManifestMajor}.",
                "package.manifest.versionUnsupported",
                PackageDiagnosticArguments.Create(
                    ("actual", version.Major.ToString(CultureInfo.InvariantCulture)),
                    ("supported", SupportedManifestMajor.ToString(CultureInfo.InvariantCulture)))));
            return;
        }

        if (version.Minor > SupportedManifestMinor)
        {
            diagnostics.Add(PackageDiagnosticBuilder.Warning(
                PackageDiagnosticCode.ManifestVersionMinorTooNew,
                PackageDiagnosticTarget.Manifest,
                $"Manifest minor version {version.Minor} is newer than the validator ({SupportedManifestMinor}); unknown additive fields will be ignored.",
                $"Upgrade the validator to manifest minor {version.Minor} or older for full fidelity.",
                "package.manifest.versionMinorTooNew",
                PackageDiagnosticArguments.Create(
                    ("actual", version.Minor.ToString(CultureInfo.InvariantCulture)),
                    ("supported", SupportedManifestMinor.ToString(CultureInfo.InvariantCulture)))));
        }
    }

    private static void ValidateVersion(string targetVersion, List<PackageDiagnostic> diagnostics)
    {
        try
        {
            var version = AppVersion.Parse(targetVersion);
            var current = EngineIdentity.Current.ProductVersion;
            if (version.Major != current.Major || version.Minor != current.Minor)
            {
                diagnostics.Add(PackageDiagnosticBuilder.Error(
                    PackageDiagnosticCode.ManifestIdentityInvalid,
                    PackageDiagnosticTarget.Manifest,
                    $"Package targets Opus {version}, but this validator is {current}.",
                    "Rebuild the package for the current Opus 0.1 toolchain.",
                    "package.manifest.versionTargetInvalid",
                    PackageDiagnosticArguments.Create(("version", version.ToString()))));
                return;
            }

            if (!string.IsNullOrWhiteSpace(version.PreRelease)
                && !string.Equals(version.PreRelease, current.PreRelease, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(PackageDiagnosticBuilder.Warning(
                    PackageDiagnosticCode.EngineChannelMismatch,
                    PackageDiagnosticTarget.Manifest,
                    $"Package release channel '{version.PreRelease}' differs from validator channel '{current.PreRelease}'.",
                    "Rebuild the package against the matching release channel.",
                    "package.manifest.channelMismatch",
                    PackageDiagnosticArguments.Create(
                        ("package", version.PreRelease),
                        ("validator", current.PreRelease))));
            }
        }
        catch (FormatException)
        {
            diagnostics.Add(PackageDiagnosticBuilder.Error(
                PackageDiagnosticCode.ManifestIdentityInvalid,
                PackageDiagnosticTarget.Manifest,
                $"Target Opus version '{targetVersion}' is not valid semver.",
                "Use semver, for example 0.1.0-alpha.",
                "package.manifest.versionMalformed",
                PackageDiagnosticArguments.Create(("version", targetVersion))));
        }
    }

    private static void ValidateRequiredFeatures(
        IReadOnlyList<string> requiredFeatures,
        List<PackageDiagnostic> diagnostics)
    {
        foreach (var feature in requiredFeatures)
        {
            if (!PackageFeatures.IsSupported(feature))
            {
                diagnostics.Add(PackageDiagnosticBuilder.Error(
                    PackageDiagnosticCode.ManifestFeatureUnsupported,
                    PackageDiagnosticTarget.Manifest,
                    $"Required package feature '{feature}' is not supported by this validator.",
                    "Remove the feature or update the validator.",
                    "package.manifest.featureUnsupported",
                    PackageDiagnosticArguments.Create(("feature", feature))));
            }
        }
    }

    private void ValidateFiles(
        string packageRoot,
        ContentPackageManifest manifest,
        PackageValidationRequest request,
        List<PackageDiagnostic> diagnostics)
    {
        var declared = new Dictionary<string, ContentPackageFile>(StringComparer.Ordinal);
        var localisationKeys = new SortedDictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        foreach (var file in manifest.Files)
        {
            if (!PackageRelativePath.TryCreate(file.Path, out var relativePath, out var reason))
            {
                diagnostics.Add(PackageDiagnosticBuilder.Error(
                    PackageDiagnosticCode.PathInvalid,
                    PackageDiagnosticTarget.Manifest,
                    $"Manifest path '{file.Path}' is invalid: {reason}",
                    "Use a normalised package-relative path without traversal.",
                    "package.path.invalid",
                    PackageDiagnosticArguments.Create(("path", file.Path), ("reason", reason))));
                continue;
            }

            if (!declared.TryAdd(relativePath.Value, file))
            {
                diagnostics.Add(PackageDiagnosticBuilder.Error(
                    PackageDiagnosticCode.PathDuplicate,
                    PackageDiagnosticTarget.File(relativePath),
                    $"Manifest declares '{relativePath.Value}' more than once.",
                    "Remove duplicate file entries.",
                    "package.path.duplicate",
                    PackageDiagnosticArguments.Create(("path", relativePath.Value))));
                continue;
            }

            _fileValidator.Validate(
                packageRoot, file, relativePath, request.MaxDeepValidationBytes, diagnostics, localisationKeys);
        }

        ValidateUnlistedFiles(packageRoot, request, declared.Keys, diagnostics);
        ValidateLocalisationKeyParity(localisationKeys, diagnostics);
    }

    private static void ValidateUnlistedFiles(
        string packageRoot,
        PackageValidationRequest request,
        IEnumerable<string> declaredPaths,
        List<PackageDiagnostic> diagnostics)
    {
        if (request.UnlistedFilePolicy == PackageUnlistedFilePolicy.Ignore)
        {
            return;
        }

        var declared = new HashSet<string>(declaredPaths, StringComparer.Ordinal);
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = false,
        };
        foreach (var file in Directory.EnumerateFiles(packageRoot, "*", options))
        {
            var relative = Path.GetRelativePath(packageRoot, file).Replace('\\', '/');
            if (relative == request.ManifestFileName
                || string.Equals(relative, DefaultSignatureFileName, StringComparison.Ordinal)
                || declared.Contains(relative))
            {
                continue;
            }

            var severity = request.UnlistedFilePolicy == PackageUnlistedFilePolicy.Error
                ? PackageDiagnosticSeverity.Error
                : PackageDiagnosticSeverity.Warning;
            diagnostics.Add(PackageDiagnostic.Create(
                severity,
                PackageDiagnosticCode.FileUnlisted,
                PackageDiagnosticTarget.File(relative),
                $"File '{relative}' exists but is not listed in the manifest.",
                "Add the file to the manifest or remove it from the package directory.",
                "package.file.unlisted",
                PackageDiagnosticArguments.Create(("path", relative))));
        }
    }

    private static void ValidateLocalisationKeyParity(
        SortedDictionary<string, IReadOnlySet<string>> localisationKeys,
        List<PackageDiagnostic> diagnostics)
    {
        if (localisationKeys.Count < 2)
        {
            return;
        }

        var reference = localisationKeys.First();
        foreach (var candidate in localisationKeys.Skip(1))
        {
            if (reference.Value.SetEquals(candidate.Value))
            {
                continue;
            }

            var missing = new SortedSet<string>(reference.Value, StringComparer.Ordinal);
            missing.ExceptWith(candidate.Value);
            var extra = new SortedSet<string>(candidate.Value, StringComparer.Ordinal);
            extra.ExceptWith(reference.Value);

            var args = PackageDiagnosticArguments.Create(
                ("path", candidate.Key),
                ("reference", reference.Key),
                ("missing", string.Join(", ", missing)),
                ("extra", string.Join(", ", extra)));

            diagnostics.Add(PackageDiagnosticBuilder.Error(
                PackageDiagnosticCode.LocalisationKeyMismatch,
                PackageDiagnosticTarget.File(candidate.Key),
                FormatParityMessage(candidate.Key, reference.Key, missing, extra),
                "Keep localisation key sets identical across package locales.",
                "package.localisation.keyMismatch",
                args));
        }
    }

    private static string FormatParityMessage(
        string candidate,
        string reference,
        IReadOnlyCollection<string> missing,
        IReadOnlyCollection<string> extra)
    {
        var details = new List<string>();
        if (missing.Count > 0)
        {
            details.Add($"missing: {string.Join(", ", missing)}");
        }

        if (extra.Count > 0)
        {
            details.Add($"extra: {string.Join(", ", extra)}");
        }

        var suffix = details.Count == 0 ? string.Empty : " (" + string.Join("; ", details) + ")";
        return $"Localisation file '{candidate}' does not share the same keys as '{reference}'{suffix}.";
    }
}
