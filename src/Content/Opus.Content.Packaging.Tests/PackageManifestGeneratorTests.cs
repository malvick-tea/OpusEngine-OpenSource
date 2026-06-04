using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Opus.Content.Packaging.Diagnostics;
using Opus.Content.Packaging.Generation;
using Opus.Content.Packaging.Manifest;
using Opus.Content.Packaging.Tests.Fixtures;
using Opus.Content.Packaging.Validation;
using Opus.Foundation;
using Xunit;

namespace Opus.Content.Packaging.Tests;

public sealed class PackageManifestGeneratorTests
{
    [Fact]
    public void Generated_manifest_round_trips_clean_through_the_validator()
    {
        using var fixture = new PackageFixtureBuilder().WithGoldenFiles();
        var result = new PackageManifestGenerator().Generate(GoldenRequest(fixture.Root));

        result.Succeeded.Should().BeTrue();
        result.Manifest.Should().NotBeNull();
        WriteManifest(fixture.Root, result.Manifest!);

        var validation = new PackageValidator().ValidateDirectory(
            PackageValidationRequest.ForDirectory(fixture.Root));

        validation.IsValid.Should().BeTrue(
            "a generated manifest must satisfy the validator it is the inverse of; diagnostics: "
            + string.Join("; ", validation.Diagnostics.Select(d => d.Code.Value + " " + d.Message)));
    }

    [Fact]
    public void Generated_manifest_lists_every_golden_file_with_matching_size_and_hash()
    {
        using var fixture = new PackageFixtureBuilder().WithGoldenFiles();
        var result = new PackageManifestGenerator().Generate(GoldenRequest(fixture.Root));

        result.Manifest!.Files.Should().HaveCount(6);
        var glb = result.Manifest.Files.Single(f => f.Path == "models/triangle.glb");
        var physicalPath = Path.Combine(fixture.Root, "models", "triangle.glb");
        glb.SizeBytes.Should().Be(new FileInfo(physicalPath).Length);
        glb.Sha256.Should().Be(PackageFileHash.ComputeSha256HexFile(physicalPath));
    }

    [Fact]
    public void Generated_manifest_infers_types_from_extension()
    {
        using var fixture = new PackageFixtureBuilder().WithGoldenFiles();
        var files = new PackageManifestGenerator().Generate(GoldenRequest(fixture.Root)).Manifest!.Files;

        TypeOf(files, "models/triangle.glb").Should().Be(PackageAssetTypes.ModelGlb);
        TypeOf(files, "textures/checker.png").Should().Be(PackageAssetTypes.TexturePng);
        TypeOf(files, "textures/minimal.ktx2").Should().Be(PackageAssetTypes.TextureKtx);
        TypeOf(files, "fonts/fixture-latin.ttf").Should().Be(PackageAssetTypes.Font);
        TypeOf(files, "localisation/en.json").Should().Be(PackageAssetTypes.LocalisationJson);
    }

    [Fact]
    public void Generated_manifest_targets_the_current_engine_identity()
    {
        using var fixture = new PackageFixtureBuilder().WithGoldenFiles();
        var manifest = new PackageManifestGenerator().Generate(GoldenRequest(fixture.Root)).Manifest!;

        manifest.FormatVersion.Major.Should().Be(PackageValidator.SupportedManifestMajor);
        manifest.FormatVersion.Minor.Should().Be(PackageValidator.SupportedManifestMinor);
        manifest.Engine.Product.Should().Be(EngineIdentity.Current.ProductName);
        manifest.Engine.TargetVersion.Should().Be(EngineIdentity.Current.ProductVersion.ToString());
    }

    [Fact]
    public void Generated_files_are_sorted_by_ordinal_path()
    {
        using var fixture = new PackageFixtureBuilder().WithGoldenFiles();
        var files = new PackageManifestGenerator().Generate(GoldenRequest(fixture.Root)).Manifest!.Files;

        files.Select(f => f.Path).Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    [Fact]
    public void Generator_skips_an_existing_manifest_file()
    {
        using var fixture = new PackageFixtureBuilder().WithGoldenFiles();
        File.WriteAllText(Path.Combine(fixture.Root, PackageValidator.DefaultManifestFileName), "{}");

        var files = new PackageManifestGenerator().Generate(GoldenRequest(fixture.Root)).Manifest!.Files;

        files.Should().NotContain(f => f.Path == PackageValidator.DefaultManifestFileName);
        files.Should().HaveCount(6);
    }

    [Fact]
    public void Generator_warns_and_omits_a_file_with_an_unknown_extension()
    {
        using var fixture = new PackageFixtureBuilder().WithGoldenFiles();
        File.WriteAllText(Path.Combine(fixture.Root, "notes.unknownext"), "scratch");

        var result = new PackageManifestGenerator().Generate(GoldenRequest(fixture.Root));

        result.Manifest!.Files.Should().NotContain(f => f.Path == "notes.unknownext");
        result.Diagnostics.Should()
            .ContainSingle(d => d.Code == PackageDiagnosticCode.GeneratedFileTypeUnknown)
            .Which.Severity.Should().Be(PackageDiagnosticSeverity.Warning);
        result.Succeeded.Should().BeTrue("a single unclassified file is a warning, not a generation failure.");
    }

    [Fact]
    public void Generator_applies_a_type_override_for_an_unmapped_extension()
    {
        using var fixture = new PackageFixtureBuilder().WithGoldenFiles();
        File.WriteAllText(Path.Combine(fixture.Root, "atlas.dat"), "payload");
        var request = GoldenRequest(fixture.Root) with
        {
            TypeOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [".dat"] = PackageAssetTypes.TexturePng,
            },
        };

        var result = new PackageManifestGenerator().Generate(request);

        TypeOf(result.Manifest!.Files, "atlas.dat").Should().Be(PackageAssetTypes.TexturePng);
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Generator_reports_a_missing_content_root()
    {
        var missing = Path.Combine(Path.GetTempPath(), "opus-no-such-package-" + Guid.NewGuid().ToString("N"));

        var result = new PackageManifestGenerator().Generate(GoldenRequest(missing));

        result.HasManifest.Should().BeFalse();
        result.Succeeded.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle(d => d.Code == PackageDiagnosticCode.PackageRootMissing);
    }

    private static PackageGenerationRequest GoldenRequest(string root) => new(
        root,
        new ContentPackageInfo(
            "vellum.opus.fixtures.generated",
            "Opus Generated Fixtures",
            "0.1.0-alpha.1",
            "2026-05-29T12:00:00Z"))
    {
        Entrypoints = new ContentPackageEntrypoints("models/triangle.glb", new[] { "en", "ru" }),
    };

    private static void WriteManifest(string root, ContentPackageManifest manifest) =>
        File.WriteAllText(
            Path.Combine(root, PackageValidator.DefaultManifestFileName),
            ContentPackageManifestReader.Write(manifest));

    private static string TypeOf(IEnumerable<ContentPackageFile> files, string path) =>
        files.Single(f => f.Path == path).Type;
}
