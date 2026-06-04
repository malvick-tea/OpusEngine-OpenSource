using System.IO;
using FluentAssertions;
using Opus.Content.Packaging.Archive;
using Opus.Content.Packaging.Diagnostics;
using Opus.Content.Packaging.Tests.Fixtures;
using Xunit;

namespace Opus.Content.Packaging.Tests.Archive;

public sealed class OpusPackageArchiveTests
{
    [Fact]
    public void Pack_then_read_round_trips_payload_entries()
    {
        using var content = new PackageFixtureBuilder().WithGoldenFiles();
        using var output = new TempDirectory();
        var archivePath = ArchivePackaging.PackGolden(content.Root, output.Path);

        OpusPackageArchiveReader.TryOpen(
            archivePath, OpusPackageArchiveLimits.Default, out var reader, out _).Should().BeTrue();
        using (reader)
        {
            reader!.HasSignature.Should().BeFalse();
            reader.PayloadEntryNames.Should().BeEquivalentTo(
                "fonts/fixture-latin.ttf",
                "localisation/en.json",
                "localisation/ru.json",
                "models/triangle.glb",
                "textures/checker.png",
                "textures/minimal.ktx2");
            reader.TryReadManifestBytes(out var manifestBytes, out _).Should().BeTrue();
            manifestBytes.Should().NotBeEmpty();
        }
    }

    [Fact]
    public void Repacking_identical_content_is_byte_identical()
    {
        using var content = new PackageFixtureBuilder().WithGoldenFiles();
        using var first = new TempDirectory();
        using var second = new TempDirectory();

        var a = ArchivePackaging.PackGolden(content.Root, first.Path);
        var b = ArchivePackaging.PackGolden(content.Root, second.Path);

        File.ReadAllBytes(a).Should().Equal(
            File.ReadAllBytes(b),
            "fixed entry timestamps and sorted entries make an .opkg reproducible.");
    }

    [Fact]
    public void Reader_rejects_a_zip_slip_entry_name()
    {
        using var output = new TempDirectory();
        var path = ArchivePackaging.WriteRawZip(
            output.Combine("evil.opkg"), ("../escape.txt", new byte[] { 1, 2, 3 }));

        var opened = OpusPackageArchiveReader.TryOpen(
            path, OpusPackageArchiveLimits.Default, out var reader, out var diagnostics);

        opened.Should().BeFalse();
        reader.Should().BeNull();
        diagnostics.Should().Contain(d => d.Code == PackageDiagnosticCode.ArchiveEntryNameUnsafe);
    }

    [Fact]
    public void Reader_rejects_an_entry_over_the_per_entry_limit()
    {
        using var output = new TempDirectory();
        var path = ArchivePackaging.WriteRawZip(output.Combine("big.opkg"), ("data.bin", new byte[2048]));
        var limits = OpusPackageArchiveLimits.Default with
        {
            MaxEntryUncompressedBytes = 1024,
            MaxTotalUncompressedBytes = 1_000_000,
            MaxCompressionRatio = 1_000_000,
        };

        OpusPackageArchiveReader.TryOpen(path, limits, out _, out var diagnostics).Should().BeFalse();
        diagnostics.Should().Contain(d => d.Code == PackageDiagnosticCode.ArchiveEntryTooLarge);
    }

    [Fact]
    public void Reader_rejects_a_total_over_the_limit()
    {
        using var output = new TempDirectory();
        var path = ArchivePackaging.WriteRawZip(
            output.Combine("total.opkg"), ("a.bin", new byte[700]), ("b.bin", new byte[700]));
        var limits = OpusPackageArchiveLimits.Default with
        {
            MaxEntryUncompressedBytes = 1024,
            MaxTotalUncompressedBytes = 1024,
            MaxCompressionRatio = 1_000_000,
        };

        OpusPackageArchiveReader.TryOpen(path, limits, out _, out var diagnostics).Should().BeFalse();
        diagnostics.Should().Contain(d => d.Code == PackageDiagnosticCode.ArchiveTooLarge);
    }

    [Fact]
    public void Reader_rejects_a_compression_bomb_ratio()
    {
        using var output = new TempDirectory();
        var path = ArchivePackaging.WriteRawZip(output.Combine("bomb.opkg"), ("zeros.bin", new byte[100_000]));
        var limits = OpusPackageArchiveLimits.Default with
        {
            MaxEntryUncompressedBytes = 10_000_000,
            MaxTotalUncompressedBytes = 10_000_000,
            MaxCompressionRatio = 2,
        };

        OpusPackageArchiveReader.TryOpen(path, limits, out _, out var diagnostics).Should().BeFalse();
        diagnostics.Should().Contain(d => d.Code == PackageDiagnosticCode.ArchiveCompressionRatioExceeded);
    }

    [Fact]
    public void Reader_reports_a_missing_manifest_entry()
    {
        using var output = new TempDirectory();
        var path = ArchivePackaging.WriteRawZip(output.Combine("nomanifest.opkg"), ("payload.bin", new byte[16]));

        OpusPackageArchiveReader.TryOpen(
            path, OpusPackageArchiveLimits.Default, out var reader, out _).Should().BeTrue();
        using (reader)
        {
            reader!.TryReadManifestBytes(out _, out var error).Should().BeFalse();
            error!.Code.Should().Be(PackageDiagnosticCode.ArchiveManifestEntryMissing);
        }
    }
}
