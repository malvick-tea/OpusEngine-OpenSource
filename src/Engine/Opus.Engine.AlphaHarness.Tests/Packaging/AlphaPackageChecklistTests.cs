using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Opus.Content.Packaging.Diagnostics;
using Opus.Content.Packaging.Manifest;
using Opus.Content.Packaging.Validation;
using Opus.Engine.AlphaHarness.Packaging;
using Opus.Foundation;
using Xunit;

namespace Opus.Engine.AlphaHarness.Tests.Packaging;

public sealed class AlphaPackageChecklistTests
{
    [Fact]
    public void Clean_manifest_with_all_features_and_locales_returns_no_errors()
    {
        var manifest = BuildAlphaReadyManifest();
        var validation = PackageValidationResult.From(manifest, Array.Empty<PackageDiagnostic>());

        var checklist = AlphaPackageChecklist.Run(manifest, validation, AlphaPackageChecklistPolicy.Default);

        checklist.IsClean.Should().BeTrue();
        checklist.ErrorCount.Should().Be(0);
        checklist.InfoCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Missing_feature_raises_error_with_stable_code()
    {
        var manifest = BuildAlphaReadyManifest() with
        {
            RequiredFeatures = new[] { PackageFeatures.Textures, PackageFeatures.Fonts, PackageFeatures.Localisation },
        };
        var validation = PackageValidationResult.From(manifest, Array.Empty<PackageDiagnostic>());

        var checklist = AlphaPackageChecklist.Run(manifest, validation, AlphaPackageChecklistPolicy.Default);

        checklist.IsClean.Should().BeFalse();
        checklist.FindingsWithCode(AlphaHarnessDiagnosticCodes.PackageRequiredFeatureMissing)
            .Where(f => f.Severity == AlphaPackageChecklistSeverity.Error)
            .Should().ContainSingle()
            .Which.Item.Should().Be($"required-feature:{PackageFeatures.Models}");
    }

    [Fact]
    public void Missing_asset_kind_raises_error()
    {
        var manifest = BuildAlphaReadyManifest();
        var withoutModels = manifest with
        {
            Files = manifest.Files.Where(file => !file.Type.StartsWith("model", StringComparison.Ordinal)).ToArray(),
        };
        var validation = PackageValidationResult.From(withoutModels, Array.Empty<PackageDiagnostic>());

        var checklist = AlphaPackageChecklist.Run(withoutModels, validation, AlphaPackageChecklistPolicy.Default);

        checklist.IsClean.Should().BeFalse();
        checklist.FindingsWithCode(AlphaHarnessDiagnosticCodes.PackageRequiredAssetKindMissing)
            .Where(f => f.Severity == AlphaPackageChecklistSeverity.Error)
            .Should().ContainSingle()
            .Which.Item.Should().Contain("model");
    }

    [Fact]
    public void Missing_locale_basename_raises_error()
    {
        var manifest = BuildAlphaReadyManifest();
        var withoutRu = manifest with
        {
            Files = manifest.Files.Where(file => !file.Path.Contains("ru", StringComparison.Ordinal)).ToArray(),
        };
        var validation = PackageValidationResult.From(withoutRu, Array.Empty<PackageDiagnostic>());

        var checklist = AlphaPackageChecklist.Run(withoutRu, validation, AlphaPackageChecklistPolicy.Default);

        checklist.IsClean.Should().BeFalse();
        checklist.FindingsWithCode(AlphaHarnessDiagnosticCodes.PackageRequiredLocaleMissing)
            .Where(f => f.Severity == AlphaPackageChecklistSeverity.Error)
            .Should().ContainSingle()
            .Which.Item.Should().Be("required-locale:ru");
    }

    [Fact]
    public void Engine_target_mismatch_raises_error()
    {
        var manifest = BuildAlphaReadyManifest();
        var divergent = manifest with
        {
            Engine = manifest.Engine with { Product = "NotOpus" },
        };
        var validation = PackageValidationResult.From(divergent, Array.Empty<PackageDiagnostic>());

        var checklist = AlphaPackageChecklist.Run(divergent, validation, AlphaPackageChecklistPolicy.Default);

        checklist.IsClean.Should().BeFalse();
        checklist.FindingsWithCode(AlphaHarnessDiagnosticCodes.PackageEngineTargetMismatch)
            .Where(f => f.Severity == AlphaPackageChecklistSeverity.Error)
            .Should().HaveCount(1);
    }

    [Fact]
    public void Null_manifest_returns_single_unavailable_finding()
    {
        var validation = PackageValidationResult.From(null, Array.Empty<PackageDiagnostic>());

        var checklist = AlphaPackageChecklist.Run(null, validation, AlphaPackageChecklistPolicy.Default);

        checklist.IsClean.Should().BeFalse();
        checklist.Findings.Should().ContainSingle()
            .Which.DiagnosticCode.Should().Be(AlphaHarnessDiagnosticCodes.PackageManifestUnavailable);
    }

    [Fact]
    public void Underlying_validation_errors_propagate_to_checklist()
    {
        var manifest = BuildAlphaReadyManifest();
        var diagnostic = PackageDiagnostic.Create(
            PackageDiagnosticSeverity.Error,
            PackageDiagnosticCode.PackageRootMissing,
            PackageDiagnosticTarget.Package,
            "package root missing",
            "supply a valid package directory",
            "package.root.missing");
        var validation = PackageValidationResult.From(manifest, new[] { diagnostic });

        var checklist = AlphaPackageChecklist.Run(manifest, validation, AlphaPackageChecklistPolicy.Default);

        checklist.FindingsWithCode(AlphaHarnessDiagnosticCodes.PackageUnderlyingValidationFailed)
            .Where(f => f.Severity == AlphaPackageChecklistSeverity.Error)
            .Should().ContainSingle();
        checklist.IsClean.Should().BeFalse();
    }

    [Fact]
    public void Findings_are_sorted_deterministically()
    {
        var manifest = BuildAlphaReadyManifest() with
        {
            RequiredFeatures = Array.Empty<string>(),
            Engine = BuildAlphaReadyManifest().Engine with { Product = "NotOpus" },
        };
        var validation = PackageValidationResult.From(manifest, Array.Empty<PackageDiagnostic>());

        var checklist = AlphaPackageChecklist.Run(manifest, validation, AlphaPackageChecklistPolicy.Default);

        var severities = checklist.Findings.Select(f => f.Severity).ToArray();
        for (var i = 1; i < severities.Length; i++)
        {
            ((int)severities[i - 1]).Should().BeGreaterThanOrEqualTo((int)severities[i]);
        }
    }

    private static ContentPackageManifest BuildAlphaReadyManifest()
    {
        var files = new[]
        {
            new ContentPackageFile(Path: "models/tank.glb", Type: PackageAssetTypes.ModelGlb, SizeBytes: 256, Sha256: "00", Metadata: null),
            new ContentPackageFile(Path: "textures/diffuse.png", Type: PackageAssetTypes.TexturePng, SizeBytes: 128, Sha256: "00", Metadata: null),
            new ContentPackageFile(Path: "fonts/hud.ttf", Type: PackageAssetTypes.Font, SizeBytes: 128, Sha256: "00", Metadata: null),
            new ContentPackageFile(Path: "localisation/en.json", Type: PackageAssetTypes.LocalisationJson, SizeBytes: 16, Sha256: "00", Metadata: null),
            new ContentPackageFile(Path: "localisation/ru.json", Type: PackageAssetTypes.LocalisationJson, SizeBytes: 16, Sha256: "00", Metadata: null),
        };
        return new ContentPackageManifest(
            FormatVersion: new ManifestFormatVersion(1, 0),
            Package: new ContentPackageInfo(
                Id: "test.package",
                DisplayName: "test",
                Version: "0.1.0-alpha",
                CreatedAtUtc: null),
            Engine: new ContentPackageTarget(
                Product: EngineIdentity.Current.ProductName,
                TargetVersion: "0.1.0",
                MinVersion: null,
                AssemblyCompatibility: null,
                TargetAdapterFamilies: Array.Empty<string>()),
            Authoring: null,
            Entrypoints: null,
            RequiredFeatures: new[]
            {
                PackageFeatures.Models,
                PackageFeatures.Textures,
                PackageFeatures.Fonts,
                PackageFeatures.Localisation,
            },
            Files: files);
    }
}
