using System.Linq;
using System.Text;
using FluentAssertions;
using Opus.Content.Packaging.Diagnostics;
using Opus.Content.Packaging.Manifest;
using Opus.Content.Packaging.Tests.Fixtures;
using Opus.Content.Packaging.Validation;
using Xunit;

namespace Opus.Content.Packaging.Tests;

/// <summary>
/// Covers the in-memory budget that bounds deep, content-aware file validation. Integrity
/// (size + SHA-256) must still be verified for files above the budget by streaming, and the
/// validator must never read an over-budget file fully into memory.
/// </summary>
public sealed class PackageValidatorLargeFileTests
{
    private static readonly string[] DeepValidationCodes =
    {
        "OPKG-MODEL-001",
        "OPKG-TEX-001",
        "OPKG-FONT-001",
        "OPKG-LOC-001",
        "OPKG-LOC-002",
    };

    [Fact]
    public void Files_above_the_budget_skip_deep_validation_with_a_warning()
    {
        using var package = new PackageFixtureBuilder()
            .WithGoldenFiles()
            .WriteManifest();

        var result = ValidateWithBudget(package.Root, maxDeepValidationBytes: 8);

        result.IsValid.Should().BeTrue("integrity passes by streaming and the budget warning is not an error");
        result.Diagnostics.Should().Contain(d =>
            d.Code == PackageDiagnosticCode.FileTooLargeForDeepValidation &&
            d.Target.Path == "models/triangle.glb" &&
            d.Severity == PackageDiagnosticSeverity.Warning);
        result.Diagnostics.Should().NotContain(d => DeepValidationCodes.Contains(d.Code.Value));
    }

    [Fact]
    public void Budget_warning_reports_the_file_size_and_budget()
    {
        using var package = new PackageFixtureBuilder()
            .WithGoldenFiles()
            .WriteManifest();

        var result = ValidateWithBudget(package.Root, maxDeepValidationBytes: 8);

        var warning = result.Diagnostics.First(d => d.Code == PackageDiagnosticCode.FileTooLargeForDeepValidation);
        warning.Arguments.Should().ContainKey("size");
        warning.Arguments["budget"].Should().Be("8");
    }

    [Fact]
    public void Integrity_is_still_verified_for_files_above_the_budget()
    {
        using var package = new PackageFixtureBuilder()
            .WithGoldenFiles()
            .WriteManifest();

        // Same byte count as the manifest declares, but one byte flipped: the size matches,
        // so only the streamed hash can catch the tamper on an over-budget file.
        var tampered = PackageFixtureBuilder.TinyPngPayload();
        tampered[^1] ^= 0x01;
        package.ReplaceFile("textures/checker.png", tampered);

        var result = ValidateWithBudget(package.Root, maxDeepValidationBytes: 8);

        result.Diagnostics.Should().Contain(d =>
            d.Code == PackageDiagnosticCode.FileHashMismatch &&
            d.Target.Path == "textures/checker.png");
        result.Diagnostics.Should().NotContain(d => d.Code == PackageDiagnosticCode.FileSizeMismatch);
        result.Diagnostics.Should().NotContain(d => d.Code == PackageDiagnosticCode.TextureInvalid);
    }

    [Fact]
    public void Default_budget_validates_normal_files_without_a_budget_warning()
    {
        using var package = new PackageFixtureBuilder()
            .WithGoldenFiles()
            .WriteManifest();

        var result = new PackageValidator().ValidateDirectory(PackageValidationRequest.ForDirectory(package.Root));

        result.IsValid.Should().BeTrue();
        result.Diagnostics.Should().NotContain(d => d.Code == PackageDiagnosticCode.FileTooLargeForDeepValidation);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void Validate_rejects_a_non_positive_budget(long budget)
    {
        using var package = new PackageFixtureBuilder()
            .WithGoldenFiles()
            .WriteManifest();

        var act = () => ValidateWithBudget(package.Root, budget);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void A_split_gltf_whose_sidecar_exceeds_the_budget_skips_deep_model_validation_with_a_warning()
    {
        using var package = new PackageFixtureBuilder()
            .AddFile("scene.gltf", PackageAssetTypes.ModelGltf, SplitGltfJson("oversized.bin"))
            .WriteManifest();

        // A small glTF pointing at an arbitrarily large sidecar is the OOM vector M11.28 closes:
        // the sidecar dwarfs the budget while the glTF JSON itself stays within it.
        File.WriteAllBytes(Path.Combine(package.Root, "oversized.bin"), new byte[64 * 1024]);
        var gltfLength = new FileInfo(Path.Combine(package.Root, "scene.gltf")).Length;

        var result = new PackageValidator().ValidateDirectory(new PackageValidationRequest(
            package.Root,
            UnlistedFilePolicy: PackageUnlistedFilePolicy.Ignore,
            MaxDeepValidationBytes: gltfLength));

        result.IsValid.Should().BeTrue("integrity is streamed, so an oversized sidecar is a warning, not an error");
        result.Diagnostics.Should().Contain(d =>
            d.Code == PackageDiagnosticCode.ModelTooLargeForDeepValidation &&
            d.Target.Path == "scene.gltf" &&
            d.Severity == PackageDiagnosticSeverity.Warning);
        result.Diagnostics.Should().NotContain(d => d.Code == PackageDiagnosticCode.ModelInvalid);

        var warning = result.Diagnostics.First(d => d.Code == PackageDiagnosticCode.ModelTooLargeForDeepValidation);
        warning.Arguments["budget"].Should().Be(gltfLength.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    // Minimal split glTF: enough JSON for the packer to resolve buffers[0].uri and check the
    // sidecar's size, which is where the over-budget refusal fires (before any mesh parse).
    private static byte[] SplitGltfJson(string sidecarUri) =>
        Encoding.UTF8.GetBytes(
            "{\"asset\":{\"version\":\"2.0\"},\"buffers\":[{\"uri\":\"" + sidecarUri + "\",\"byteLength\":4}]}");

    private static PackageValidationResult ValidateWithBudget(string root, long maxDeepValidationBytes) =>
        new PackageValidator().ValidateDirectory(
            new PackageValidationRequest(root, MaxDeepValidationBytes: maxDeepValidationBytes));
}
