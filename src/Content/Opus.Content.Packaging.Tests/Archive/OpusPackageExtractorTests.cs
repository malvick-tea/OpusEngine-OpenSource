using System.IO;
using System.Security.Cryptography;
using FluentAssertions;
using Opus.Content.Packaging.Archive;
using Opus.Content.Packaging.Diagnostics;
using Opus.Content.Packaging.Tests.Fixtures;
using Opus.Content.Packaging.Validation;
using Xunit;

namespace Opus.Content.Packaging.Tests.Archive;

public sealed class OpusPackageExtractorTests
{
    [Fact]
    public void Extract_reproduces_payload_bytes()
    {
        using var content = new PackageFixtureBuilder().WithGoldenFiles();
        using var output = new TempDirectory();
        using var target = new TempDirectory();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var archivePath = ArchivePackaging.PackGolden(
            content.Root,
            output.Path,
            key,
            "extractor-tests");

        var result = Extract(archivePath, target.Path, key);

        result.Succeeded.Should().BeTrue();
        var extracted = Path.Combine(target.Path, "textures", "checker.png");
        var source = Path.Combine(content.Root, "textures", "checker.png");
        File.ReadAllBytes(extracted).Should().Equal(File.ReadAllBytes(source));
    }

    [Fact]
    public void Extracted_package_validates_clean()
    {
        using var content = new PackageFixtureBuilder().WithGoldenFiles();
        using var output = new TempDirectory();
        using var target = new TempDirectory();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var archivePath = ArchivePackaging.PackGolden(
            content.Root,
            output.Path,
            key,
            "extractor-tests");

        Extract(archivePath, target.Path, key)
            .Succeeded.Should().BeTrue();

        var validation = new PackageValidator().ValidateDirectory(
            PackageValidationRequest.ForDirectory(target.Path));
        validation.IsValid.Should().BeTrue(
            "an unpacked package must validate through the directory validator; diagnostics: "
            + string.Join("; ", validation.Diagnostics.Select(d => d.Code.Value + " " + d.Message)));
    }

    [Fact]
    public void Extractor_rejects_a_zip_slip_archive()
    {
        using var source = new TempDirectory();
        using var target = new TempDirectory();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var archivePath = ArchivePackaging.WriteRawZip(
            source.Combine("evil.opkg"), ("../escape.txt", new byte[] { 1, 2, 3 }));

        var result = Extract(archivePath, target.Path, key);

        result.Succeeded.Should().BeFalse();
        result.Diagnostics.Should().Contain(d => d.Code == PackageDiagnosticCode.ArchiveEntryNameUnsafe);
        File.Exists(Path.Combine(Path.GetDirectoryName(target.Path)!, "escape.txt")).Should().BeFalse();
    }

    [Fact]
    public void Extractor_rejects_case_colliding_entries()
    {
        using var source = new TempDirectory();
        using var target = new TempDirectory();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var archivePath = ArchivePackaging.WriteRawZip(
            source.Combine("collision.opkg"),
            ("Models/Tank.glb", new byte[] { 1 }),
            ("models/tank.glb", new byte[] { 2 }));

        var result = Extract(archivePath, target.Path, key);

        result.Succeeded.Should().BeFalse();
        result.Diagnostics.Should().Contain(
            diagnostic => diagnostic.Code == PackageDiagnosticCode.ArchiveMalformed);
    }

    [Fact]
    public void Extractor_rejects_a_non_empty_target()
    {
        using var content = new PackageFixtureBuilder().WithGoldenFiles();
        using var output = new TempDirectory();
        using var target = new TempDirectory();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        File.WriteAllText(target.Combine("stale.txt"), "unverified");
        var archivePath = ArchivePackaging.PackGolden(
            content.Root,
            output.Path,
            key,
            "extractor-tests");

        var result = Extract(archivePath, target.Path, key);

        result.Succeeded.Should().BeFalse();
        result.Diagnostics.Should().Contain(
            diagnostic => diagnostic.Code == PackageDiagnosticCode.ArchiveTargetNotEmpty);
    }

    [Fact]
    public void Extractor_rejects_an_unsigned_archive_before_writing_payloads()
    {
        using var content = new PackageFixtureBuilder().WithGoldenFiles();
        using var output = new TempDirectory();
        using var target = new TempDirectory();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var archivePath = ArchivePackaging.PackGolden(content.Root, output.Path);

        var result = Extract(archivePath, target.Path, key);

        result.Succeeded.Should().BeFalse();
        result.Diagnostics.Should().Contain(
            diagnostic => diagnostic.Code == PackageDiagnosticCode.SignatureMissing);
        Directory.EnumerateFileSystemEntries(target.Path).Should().BeEmpty();
    }

    private static PackageArchiveExtractionResult Extract(
        string archivePath,
        string targetDirectory,
        ECDsa publicKey) =>
        OpusPackageExtractor.Extract(
            new PackageArchiveVerifyRequest(archivePath)
            {
                PublicKey = publicKey,
                RequireSignature = true,
            },
            targetDirectory);
}
