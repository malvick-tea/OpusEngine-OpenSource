using System.IO;
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
        var archivePath = ArchivePackaging.PackGolden(content.Root, output.Path);

        var result = OpusPackageExtractor.Extract(archivePath, target.Path, OpusPackageArchiveLimits.Default);

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
        var archivePath = ArchivePackaging.PackGolden(content.Root, output.Path);

        OpusPackageExtractor.Extract(archivePath, target.Path, OpusPackageArchiveLimits.Default)
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
        var archivePath = ArchivePackaging.WriteRawZip(
            source.Combine("evil.opkg"), ("../escape.txt", new byte[] { 1, 2, 3 }));

        var result = OpusPackageExtractor.Extract(archivePath, target.Path, OpusPackageArchiveLimits.Default);

        result.Succeeded.Should().BeFalse();
        result.Diagnostics.Should().Contain(d => d.Code == PackageDiagnosticCode.ArchiveEntryNameUnsafe);
        File.Exists(Path.Combine(Path.GetDirectoryName(target.Path)!, "escape.txt")).Should().BeFalse();
    }
}
