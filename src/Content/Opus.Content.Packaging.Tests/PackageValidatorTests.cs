using System.Linq;
using System.Text;
using FluentAssertions;
using Opus.Content.Packaging.Diagnostics;
using Opus.Content.Packaging.Manifest;
using Opus.Content.Packaging.Paths;
using Opus.Content.Packaging.Tests.Fixtures;
using Opus.Content.Packaging.Validation;
using Xunit;

namespace Opus.Content.Packaging.Tests;

public sealed class PackageValidatorTests
{
    [Fact]
    public void ValidateDirectory_accepts_golden_generic_package()
    {
        using var package = new PackageFixtureBuilder()
            .WithGoldenFiles()
            .WriteManifest();

        var result = Validate(package.Root);

        result.IsValid.Should().BeTrue(string.Join(Environment.NewLine, result.Diagnostics.Select(d => $"{d.Code}: {d.Message}")));
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void ValidateDirectory_reports_missing_package_root()
    {
        var nonExistent = Path.Combine(Path.GetTempPath(), $"opus-package-missing-{Guid.NewGuid():N}");

        var result = Validate(nonExistent);

        result.IsValid.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle(d => d.Code == PackageDiagnosticCode.PackageRootMissing);
    }

    [Fact]
    public void ValidateDirectory_reports_missing_manifest()
    {
        using var package = new PackageFixtureBuilder();

        var result = Validate(package.Root);

        result.IsValid.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle(d => d.Code == PackageDiagnosticCode.ManifestMissing);
    }

    [Fact]
    public void ValidateDirectory_reports_malformed_manifest()
    {
        using var package = new PackageFixtureBuilder();
        package.WriteMalformedManifest();

        var result = Validate(package.Root);

        result.IsValid.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle(d => d.Code == PackageDiagnosticCode.ManifestMalformed);
    }

    [Fact]
    public void ValidateDirectory_emits_warning_for_newer_manifest_minor()
    {
        using var package = new PackageFixtureBuilder()
            .WithGoldenFiles()
            .WithManifestFormatVersion(1, 9)
            .WriteManifest();

        var result = Validate(package.Root);

        result.IsValid.Should().BeTrue();
        result.Diagnostics.Should().ContainSingle(d => d.Code == PackageDiagnosticCode.ManifestVersionMinorTooNew
            && d.Severity == PackageDiagnosticSeverity.Warning);
    }

    [Fact]
    public void ValidateDirectory_rejects_unsupported_manifest_major()
    {
        using var package = new PackageFixtureBuilder()
            .WithGoldenFiles()
            .WithManifestFormatVersion(99, 0)
            .WriteManifest();

        var result = Validate(package.Root);

        result.IsValid.Should().BeFalse();
        result.Diagnostics.Should().Contain(d => d.Code == PackageDiagnosticCode.ManifestVersionUnsupported);
    }

    [Fact]
    public void ValidateDirectory_rejects_wrong_engine_product()
    {
        using var package = new PackageFixtureBuilder()
            .WithGoldenFiles()
            .WithEngineProduct("Unreal")
            .WriteManifest();

        var result = Validate(package.Root);

        result.IsValid.Should().BeFalse();
        result.Diagnostics.Should().Contain(d => d.Code == PackageDiagnosticCode.ManifestIdentityInvalid);
    }

    [Fact]
    public void ValidateDirectory_rejects_wrong_engine_major_minor()
    {
        using var package = new PackageFixtureBuilder()
            .WithGoldenFiles()
            .WithEngineTargetVersion("9.9.0-alpha")
            .WriteManifest();

        var result = Validate(package.Root);

        result.IsValid.Should().BeFalse();
        result.Diagnostics.Should().Contain(d => d.Code == PackageDiagnosticCode.ManifestIdentityInvalid);
    }

    [Fact]
    public void ValidateDirectory_warns_on_release_channel_mismatch()
    {
        using var package = new PackageFixtureBuilder()
            .WithGoldenFiles()
            .WithEngineTargetVersion("0.1.0-beta")
            .WriteManifest();

        var result = Validate(package.Root);

        result.Diagnostics.Should().Contain(d => d.Code == PackageDiagnosticCode.EngineChannelMismatch
            && d.Severity == PackageDiagnosticSeverity.Warning);
    }

    [Theory]
    [InlineData("../escape.bin", "parent-directory")]
    [InlineData("/rooted.bin", "relative to the package root")]
    [InlineData("nested//empty.bin", "empty segment")]
    public void ValidateDirectory_rejects_unsafe_paths(string declaredPath, string reasonFragment)
    {
        using var package = new PackageFixtureBuilder()
            .WithGoldenFiles()
            .AddUnsafePath(declaredPath)
            .WriteManifest();

        var result = Validate(package.Root);

        result.IsValid.Should().BeFalse();
        result.Diagnostics.Should().Contain(d =>
            d.Code == PackageDiagnosticCode.PathInvalid &&
            d.Arguments["reason"].Contains(reasonFragment, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateDirectory_reports_size_mismatch_separately_from_hash()
    {
        using var package = new PackageFixtureBuilder()
            .WithGoldenFiles()
            .WriteManifest();
        package.ReplaceFile("textures/checker.png", new byte[] { 1, 2, 3, 4, 5, 6 });

        var result = Validate(package.Root);

        result.Diagnostics.Should().Contain(d => d.Code == PackageDiagnosticCode.FileSizeMismatch);
        result.Diagnostics.Should().Contain(d => d.Code == PackageDiagnosticCode.FileHashMismatch);
    }

    [Fact]
    public void ValidateDirectory_reports_hash_mismatch_per_file()
    {
        using var package = new PackageFixtureBuilder()
            .WithGoldenFiles()
            .WriteManifest();

        // Same size as the manifest declares, but with one byte flipped so the SHA-256
        // diverges. This isolates the hash diagnostic from the size diagnostic.
        var tampered = PackageFixtureBuilder.TinyPngPayload();
        tampered[^1] ^= 0x01;
        package.ReplaceFile("textures/checker.png", tampered);

        var result = Validate(package.Root);

        result.Diagnostics.Should().Contain(d =>
            d.Code == PackageDiagnosticCode.FileHashMismatch &&
            d.Target.Path == "textures/checker.png");
        result.Diagnostics.Should().NotContain(d => d.Code == PackageDiagnosticCode.FileSizeMismatch);
    }

    [Fact]
    public void ValidateDirectory_reports_missing_declared_file()
    {
        using var package = new PackageFixtureBuilder()
            .WithGoldenFiles()
            .WriteManifest();
        package.DeleteFile("models/triangle.glb");

        var result = Validate(package.Root);

        result.Diagnostics.Should().Contain(d =>
            d.Code == PackageDiagnosticCode.FileMissing &&
            d.Target.Path == "models/triangle.glb");
    }

    [Fact]
    public void ValidateDirectory_reports_duplicate_paths()
    {
        using var package = new PackageFixtureBuilder()
            .WithGoldenFiles()
            .DuplicateLastFileEntry()
            .WriteManifest();

        var result = Validate(package.Root);

        result.Diagnostics.Should().Contain(d => d.Code == PackageDiagnosticCode.PathDuplicate);
    }

    [Fact]
    public void ValidateDirectory_reports_unsupported_required_feature()
    {
        using var package = new PackageFixtureBuilder()
            .WithGoldenFiles()
            .WithRequiredFeature("signed-archive")
            .WriteManifest();

        var result = Validate(package.Root);

        result.Diagnostics.Should().ContainSingle(d => d.Code == PackageDiagnosticCode.ManifestFeatureUnsupported);
    }

    [Fact]
    public void ValidateDirectory_reports_unlisted_files_as_warning_by_default()
    {
        using var package = new PackageFixtureBuilder()
            .WithGoldenFiles()
            .WriteManifest();
        File.WriteAllText(Path.Combine(package.Root, "loose.txt"), "not declared");

        var result = Validate(package.Root);

        result.Diagnostics.Should().ContainSingle(d =>
            d.Code == PackageDiagnosticCode.FileUnlisted &&
            d.Severity == PackageDiagnosticSeverity.Warning);
    }

    [Fact]
    public void ValidateDirectory_can_treat_unlisted_files_as_errors()
    {
        using var package = new PackageFixtureBuilder()
            .WithGoldenFiles()
            .WriteManifest();
        File.WriteAllText(Path.Combine(package.Root, "loose.txt"), "not declared");

        var result = new PackageValidator().ValidateDirectory(new PackageValidationRequest(
            package.Root,
            UnlistedFilePolicy: PackageUnlistedFilePolicy.Error));

        result.IsValid.Should().BeFalse();
        result.Diagnostics.Should().ContainSingle(d =>
            d.Code == PackageDiagnosticCode.FileUnlisted &&
            d.Severity == PackageDiagnosticSeverity.Error);
    }

    [Fact]
    public void ValidateDirectory_reports_corrupt_model_file()
    {
        using var package = new PackageFixtureBuilder()
            .WithGoldenFiles()
            .WriteManifest();
        package.ReplaceFile("models/triangle.glb", new byte[] { 0, 1, 2, 3 });

        var result = Validate(package.Root);

        result.Diagnostics.Should().Contain(d => d.Code == PackageDiagnosticCode.ModelInvalid);
    }

    [Fact]
    public void ValidateDirectory_reports_corrupt_texture_file()
    {
        using var package = new PackageFixtureBuilder()
            .WithGoldenFiles()
            .WriteManifest();
        package.ReplaceFile("textures/checker.png", new byte[] { 0, 1, 2, 3 });

        var result = Validate(package.Root);

        result.Diagnostics.Should().Contain(d => d.Code == PackageDiagnosticCode.TextureInvalid);
    }

    [Fact]
    public void ValidateDirectory_reports_corrupt_font_file()
    {
        using var package = new PackageFixtureBuilder()
            .WithGoldenFiles()
            .WriteManifest();
        package.ReplaceFile("fonts/fixture-latin.ttf", new byte[] { 0, 1, 2, 3 });

        var result = Validate(package.Root);

        result.Diagnostics.Should().Contain(d => d.Code == PackageDiagnosticCode.FontInvalid);
    }

    [Fact]
    public void ValidateDirectory_reports_localisation_key_mismatch_with_missing_and_extra_keys()
    {
        using var package = new PackageFixtureBuilder()
            .WithGoldenFiles()
            .WriteManifest();
        package.ReplaceFile(
            "localisation/ru.json",
            Encoding.UTF8.GetBytes("""{"sample.title":"Опус","sample.extra":"Extra"}"""));

        var result = Validate(package.Root);

        var parity = result.Diagnostics.Should()
            .Contain(d => d.Code == PackageDiagnosticCode.LocalisationKeyMismatch).Which;
        parity.Arguments["missing"].Should().Contain("sample.ok");
        parity.Arguments["extra"].Should().Contain("sample.extra");
    }

    [Fact]
    public void ValidateDirectory_reads_localisation_json_with_utf8_bom()
    {
        using var package = new PackageFixtureBuilder()
            .WithGoldenFiles()
            .WriteManifest();
        var withBom = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes("""{"sample.title":"Опус","sample.ok":"OK"}"""))
            .ToArray();
        package.ReplaceFile("localisation/ru.json", withBom);

        var result = Validate(package.Root);

        result.Diagnostics.Should().NotContain(d => d.Code == PackageDiagnosticCode.LocalisationInvalid);
    }

    [Fact]
    public void ValidateDirectory_reports_unsupported_file_type()
    {
        using var package = new PackageFixtureBuilder()
            .WithGoldenFiles()
            .AddFile("data/blob.bin", "unknown.blob", new byte[] { 1, 2, 3 })
            .WriteManifest();

        var result = Validate(package.Root);

        result.Diagnostics.Should().ContainSingle(d => d.Code == PackageDiagnosticCode.FileTypeUnsupported);
    }

    [Fact]
    public void ValidateDirectory_orders_diagnostics_by_severity_then_code()
    {
        using var package = new PackageFixtureBuilder()
            .WithGoldenFiles()
            .WithRequiredFeature("not-a-feature")
            .WriteManifest();
        File.WriteAllText(Path.Combine(package.Root, "loose.txt"), "noise");

        var result = Validate(package.Root);

        result.Diagnostics
            .Select(d => d.Severity)
            .Should()
            .BeInDescendingOrder();
    }

    [Fact]
    public void Manifest_round_trip_preserves_unknown_root_extension_fields()
    {
        var json = """
            {
              "formatVersion": { "major": 1, "minor": 0 },
              "package": { "id": "x", "displayName": "X", "version": "0.1.0", "createdAtUtc": null },
              "engine": { "product": "Opus", "targetVersion": "0.1.0-alpha", "minVersion": null, "assemblyCompatibility": null, "targetAdapterFamilies": [] },
              "authoring": null,
              "entrypoints": null,
              "requiredFeatures": [],
              "files": [],
              "futureField": { "experimental": true }
            }
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var result = ContentPackageManifestReader.Read(stream);

        result.IsOk.Should().BeTrue();
        var manifest = result.Unwrap();
        manifest.ExtensionData.Should().NotBeNull();
        manifest.ExtensionData!.Should().ContainKey("futureField");
    }

    [Fact]
    public void Streaming_hash_matches_in_memory_hash()
    {
        var payload = Enumerable.Range(0, 200_000).Select(i => (byte)(i & 0xFF)).ToArray();
        var path = Path.Combine(Path.GetTempPath(), $"opus-hash-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(path, payload);
        try
        {
            var inMemory = PackageFileHash.ComputeSha256Hex(payload);
            var streamed = PackageFileHash.ComputeSha256HexFile(path);

            streamed.Should().Be(inMemory);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("name.txt", true, null)]
    [InlineData("dir/inner.txt", true, null)]
    [InlineData("dir\\inner.txt", true, "dir/inner.txt")]
    [InlineData("", false, null)]
    [InlineData("  ", false, null)]
    [InlineData("/abs.txt", false, null)]
    [InlineData("../escape.txt", false, null)]
    [InlineData("a/./b.txt", false, null)]
    [InlineData("a/b\0.txt", false, null)]
    public void PackageRelativePath_accepts_safe_inputs_and_rejects_unsafe_ones(
        string input,
        bool expectedValid,
        string? expectedNormalised)
    {
        var ok = PackageRelativePath.TryCreate(input, out var path, out _);

        ok.Should().Be(expectedValid);
        if (expectedValid)
        {
            path.Value.Should().Be(expectedNormalised ?? input);
        }
    }

    private static PackageValidationResult Validate(string root) =>
        new PackageValidator().ValidateDirectory(PackageValidationRequest.ForDirectory(root));
}
