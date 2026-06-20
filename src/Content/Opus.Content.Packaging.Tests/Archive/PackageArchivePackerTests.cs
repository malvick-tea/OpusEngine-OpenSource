using FluentAssertions;
using Opus.Content.Packaging.Archive;
using Opus.Content.Packaging.Diagnostics;
using Opus.Content.Packaging.Tests.Fixtures;
using Xunit;

namespace Opus.Content.Packaging.Tests.Archive;

public sealed class PackageArchivePackerTests
{
    [Fact]
    public void Pack_rejects_content_changed_after_manifest_generation()
    {
        using var content = new PackageFixtureBuilder().WithGoldenFiles();
        using var output = new TempDirectory();
        var manifest = ArchivePackaging.GenerateManifest(content.Root);
        content.ReplaceFile("textures/checker.png", new byte[] { 1, 2, 3, 4 });
        var outputPath = output.Combine("changed.opkg");

        var result = PackageArchivePacker.Pack(
            new PackageArchivePackRequest(content.Root, manifest, outputPath));

        result.Succeeded.Should().BeFalse();
        result.Diagnostics.Should().Contain(
            diagnostic => diagnostic.Code == PackageDiagnosticCode.ArchiveWriteFailed);
        File.Exists(outputPath).Should().BeFalse();
    }

    [Fact]
    public void Pack_rejects_duplicate_case_insensitive_paths()
    {
        using var content = new PackageFixtureBuilder().WithGoldenFiles();
        using var output = new TempDirectory();
        var manifest = ArchivePackaging.GenerateManifest(content.Root);
        var duplicate = manifest.Files[0] with { Path = manifest.Files[0].Path.ToUpperInvariant() };
        manifest = manifest with { Files = manifest.Files.Append(duplicate).ToArray() };

        var result = PackageArchivePacker.Pack(
            new PackageArchivePackRequest(content.Root, manifest, output.Combine("duplicate.opkg")));

        result.Succeeded.Should().BeFalse();
        result.Diagnostics.Should().Contain(
            diagnostic => diagnostic.Code == PackageDiagnosticCode.PathDuplicate);
    }
}
