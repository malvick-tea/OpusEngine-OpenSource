using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Opus.Content.Packaging.Diagnostics;
using Opus.Content.Packaging.Manifest;
using Opus.Content.Packaging.Validation;

namespace Opus.Engine.AlphaHarness.Packaging;

/// <summary>
/// Runs the M9 alpha-package checklist over a <see cref="ContentPackageManifest"/> and
/// the matching <see cref="PackageValidationResult"/> produced by the M6 validator. The
/// checklist is a pure function: it observes inputs, returns a deterministic
/// <see cref="AlphaPackageChecklistResult"/>, and never touches disk. Hosts call it after
/// the validator pass so they can decide whether a candidate package is alpha-ready.
/// </summary>
public static class AlphaPackageChecklist
{
    /// <summary>Runs the checklist against a manifest + validation result pair using the
    /// supplied <paramref name="policy"/>. Pass <see cref="AlphaPackageChecklistPolicy.Default"/>
    /// for the canonical Opus 0.1 policy.</summary>
    public static AlphaPackageChecklistResult Run(
        ContentPackageManifest? manifest,
        PackageValidationResult validation,
        AlphaPackageChecklistPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(validation);
        ArgumentNullException.ThrowIfNull(policy);
        policy.Validate();

        var findings = new List<AlphaPackageChecklistFinding>();
        if (manifest is null)
        {
            findings.Add(ManifestUnavailable(validation));
            return AlphaPackageChecklistResult.From(findings);
        }

        AppendEngineTarget(findings, manifest, policy);
        AppendRequiredFeatures(findings, manifest, policy);
        AppendRequiredAssetKinds(findings, manifest, policy);
        AppendRequiredLocales(findings, manifest, policy);
        AppendUnderlyingValidation(findings, validation);
        return AlphaPackageChecklistResult.From(findings);
    }

    private static AlphaPackageChecklistFinding ManifestUnavailable(PackageValidationResult validation)
    {
        var firstError = validation.Diagnostics
            .FirstOrDefault(static diagnostic => diagnostic.Severity == PackageDiagnosticSeverity.Error);
        var message = BuildManifestUnavailableMessage(firstError);
        return new AlphaPackageChecklistFinding(
            DiagnosticCode: AlphaHarnessDiagnosticCodes.PackageManifestUnavailable,
            Severity: AlphaPackageChecklistSeverity.Error,
            Item: "manifest",
            Message: message);
    }

    private static string BuildManifestUnavailableMessage(PackageDiagnostic? firstError)
    {
        if (firstError is null)
        {
            return "Manifest could not be loaded; checklist has nothing to inspect.";
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"Manifest could not be loaded ({firstError.Code.Value}: {firstError.Message}).");
    }

    private static void AppendEngineTarget(
        List<AlphaPackageChecklistFinding> findings,
        ContentPackageManifest manifest,
        AlphaPackageChecklistPolicy policy)
    {
        var actual = manifest.Engine.Product;
        if (string.Equals(actual, policy.EngineProductName, StringComparison.Ordinal))
        {
            findings.Add(new AlphaPackageChecklistFinding(
                DiagnosticCode: AlphaHarnessDiagnosticCodes.PackageEngineTargetMismatch,
                Severity: AlphaPackageChecklistSeverity.Info,
                Item: $"engine-target:{policy.EngineProductName}",
                Message: $"Engine target matches '{policy.EngineProductName}'."));
            return;
        }

        findings.Add(new AlphaPackageChecklistFinding(
            DiagnosticCode: AlphaHarnessDiagnosticCodes.PackageEngineTargetMismatch,
            Severity: AlphaPackageChecklistSeverity.Error,
            Item: $"engine-target:{policy.EngineProductName}",
            Message: $"Engine target '{actual ?? "<missing>"}' does not match required '{policy.EngineProductName}'."));
    }

    private static void AppendRequiredFeatures(
        List<AlphaPackageChecklistFinding> findings,
        ContentPackageManifest manifest,
        AlphaPackageChecklistPolicy policy)
    {
        var declared = new HashSet<string>(manifest.RequiredFeatures ?? Array.Empty<string>(), StringComparer.Ordinal);
        foreach (var required in policy.RequiredFeatures)
        {
            if (declared.Contains(required))
            {
                findings.Add(new AlphaPackageChecklistFinding(
                    DiagnosticCode: AlphaHarnessDiagnosticCodes.PackageRequiredFeatureMissing,
                    Severity: AlphaPackageChecklistSeverity.Info,
                    Item: $"required-feature:{required}",
                    Message: $"Manifest declares required feature '{required}'."));
                continue;
            }

            findings.Add(new AlphaPackageChecklistFinding(
                DiagnosticCode: AlphaHarnessDiagnosticCodes.PackageRequiredFeatureMissing,
                Severity: AlphaPackageChecklistSeverity.Error,
                Item: $"required-feature:{required}",
                Message: $"Manifest is missing required feature '{required}'."));
        }
    }

    private static void AppendRequiredAssetKinds(
        List<AlphaPackageChecklistFinding> findings,
        ContentPackageManifest manifest,
        AlphaPackageChecklistPolicy policy)
    {
        foreach (var kind in policy.RequiredAssetKinds)
        {
            var present = manifest.Files.Any(file => MatchesAssetKind(file, kind));
            var severity = present ? AlphaPackageChecklistSeverity.Info : AlphaPackageChecklistSeverity.Error;
            var message = present
                ? $"Manifest declares at least one '{kind}' asset."
                : $"Manifest declares no '{kind}' assets — alpha package requires one.";
            findings.Add(new AlphaPackageChecklistFinding(
                DiagnosticCode: AlphaHarnessDiagnosticCodes.PackageRequiredAssetKindMissing,
                Severity: severity,
                Item: $"required-asset:{kind}".ToLowerInvariant(),
                Message: message));
        }
    }

    private static bool MatchesAssetKind(ContentPackageFile file, AlphaPackageRequiredAssetKind kind) => kind switch
    {
        AlphaPackageRequiredAssetKind.Model =>
            string.Equals(file.Type, PackageAssetTypes.ModelGlb, StringComparison.Ordinal)
            || string.Equals(file.Type, PackageAssetTypes.ModelGltf, StringComparison.Ordinal),
        AlphaPackageRequiredAssetKind.Texture =>
            string.Equals(file.Type, PackageAssetTypes.TexturePng, StringComparison.Ordinal)
            || string.Equals(file.Type, PackageAssetTypes.TextureJpeg, StringComparison.Ordinal)
            || string.Equals(file.Type, PackageAssetTypes.TextureKtx, StringComparison.Ordinal),
        AlphaPackageRequiredAssetKind.Font =>
            string.Equals(file.Type, PackageAssetTypes.Font, StringComparison.Ordinal),
        AlphaPackageRequiredAssetKind.Localisation =>
            string.Equals(file.Type, PackageAssetTypes.LocalisationJson, StringComparison.Ordinal)
            || string.Equals(file.Type, PackageAssetTypes.LocalisationCsv, StringComparison.Ordinal),
        _ => false,
    };

    private static void AppendRequiredLocales(
        List<AlphaPackageChecklistFinding> findings,
        ContentPackageManifest manifest,
        AlphaPackageChecklistPolicy policy)
    {
        if (policy.RequiredLocales.Count == 0)
        {
            return;
        }

        var localeBasenames = manifest.Files
            .Where(static file => IsLocalisationFile(file))
            .Select(static file => Path.GetFileNameWithoutExtension(file.Path) ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var locale in policy.RequiredLocales)
        {
            var present = localeBasenames.Contains(locale);
            var severity = present ? AlphaPackageChecklistSeverity.Info : AlphaPackageChecklistSeverity.Error;
            var message = present
                ? $"Localisation locale '{locale}' present in manifest."
                : $"Localisation locale '{locale}' missing from manifest entries.";
            findings.Add(new AlphaPackageChecklistFinding(
                DiagnosticCode: AlphaHarnessDiagnosticCodes.PackageRequiredLocaleMissing,
                Severity: severity,
                Item: $"required-locale:{locale}",
                Message: message));
        }
    }

    private static bool IsLocalisationFile(ContentPackageFile file) =>
        string.Equals(file.Type, PackageAssetTypes.LocalisationJson, StringComparison.Ordinal)
        || string.Equals(file.Type, PackageAssetTypes.LocalisationCsv, StringComparison.Ordinal);

    private static void AppendUnderlyingValidation(
        List<AlphaPackageChecklistFinding> findings,
        PackageValidationResult validation)
    {
        if (validation.ErrorCount == 0)
        {
            findings.Add(new AlphaPackageChecklistFinding(
                DiagnosticCode: AlphaHarnessDiagnosticCodes.PackageUnderlyingValidationFailed,
                Severity: AlphaPackageChecklistSeverity.Info,
                Item: "underlying-validation",
                Message: "Underlying content validator reported no errors."));
            return;
        }

        findings.Add(new AlphaPackageChecklistFinding(
            DiagnosticCode: AlphaHarnessDiagnosticCodes.PackageUnderlyingValidationFailed,
            Severity: AlphaPackageChecklistSeverity.Error,
            Item: "underlying-validation",
            Message: $"Underlying content validator reported {validation.ErrorCount} error diagnostic(s)."));
    }
}
