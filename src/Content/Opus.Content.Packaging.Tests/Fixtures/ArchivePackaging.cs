using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using FluentAssertions;
using Opus.Content.Packaging.Archive;
using Opus.Content.Packaging.Generation;
using Opus.Content.Packaging.Manifest;

namespace Opus.Content.Packaging.Tests.Fixtures;

/// <summary>Shared helpers for archive tests: pack a golden content tree into a <c>.opkg</c>, and
/// build or mutate raw ZIP archives to exercise the reader's hostile-input guards.</summary>
internal static class ArchivePackaging
{
    public static ContentPackageManifest GenerateManifest(string contentRoot) =>
        new PackageManifestGenerator()
            .Generate(new PackageGenerationRequest(contentRoot, Info()))
            .Manifest!;

    public static string PackGolden(string contentRoot, string outputDirectory, ECDsa? key = null, string? keyId = null)
    {
        var manifest = GenerateManifest(contentRoot);
        var output = System.IO.Path.Combine(outputDirectory, "package" + OpusPackageArchive.FileExtension);
        var result = PackageArchivePacker.Pack(new PackageArchivePackRequest(contentRoot, manifest, output)
        {
            SigningKey = key,
            SigningKeyId = keyId,
        });
        result.Succeeded.Should().BeTrue(
            "packing a golden tree must succeed; diagnostics: "
            + string.Join("; ", result.Diagnostics.Select(d => d.Code.Value)));
        return output;
    }

    public static string WriteRawZip(string path, params (string Name, byte[] Bytes)[] entries)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create);
        foreach (var (name, bytes) in entries)
        {
            var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            entryStream.Write(bytes, 0, bytes.Length);
        }

        return path;
    }

    public static void ReplaceZipEntry(string archivePath, string entryName, byte[] newBytes)
    {
        using var zip = ZipFile.Open(archivePath, ZipArchiveMode.Update);
        zip.GetEntry(entryName)?.Delete();
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(newBytes, 0, newBytes.Length);
    }

    public static void AddZipEntry(string archivePath, string entryName, byte[] bytes)
    {
        using var zip = ZipFile.Open(archivePath, ZipArchiveMode.Update);
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(bytes, 0, bytes.Length);
    }

    public static byte[] ReadZipEntry(string archivePath, string entryName)
    {
        using var zip = ZipFile.OpenRead(archivePath);
        using var stream = zip.GetEntry(entryName)!.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static ContentPackageInfo Info() => new(
        "vellum.opus.fixtures.archive", "Opus Archive Fixtures", "0.1.0-alpha.1", "2026-05-30T00:00:00Z");
}
