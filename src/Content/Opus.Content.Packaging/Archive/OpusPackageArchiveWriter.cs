using System.IO.Compression;
using Opus.Content.Packaging.Manifest;

namespace Opus.Content.Packaging.Archive;

/// <summary>
/// Writes a <c>.opkg</c> archive: the reserved manifest (and optional signature) entries plus
/// every manifest-declared payload file, added in a fixed order with a fixed timestamp so
/// repacking identical content yields a stable artifact. The caller has already validated the
/// manifest paths and the size budget; the writer's job is purely to lay out the ZIP, and it
/// stores the exact <paramref name="manifestBytes"/> that were signed so the on-disk manifest
/// matches the signature.
/// </summary>
public static class OpusPackageArchiveWriter
{
    // ZIP encodes DOS timestamps from 1980; pin every entry to that epoch so the archive does not
    // leak wall-clock build time and repacking the same content is reproducible.
    private static readonly DateTimeOffset FixedEntryTimestamp = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Writes the archive to <paramref name="destination"/>, leaving the stream open for
    /// the caller (which owns the destination file and its atomic rename).</summary>
    public static void Write(
        string contentRoot,
        ContentPackageManifest manifest,
        byte[] manifestBytes,
        byte[]? signatureBytes,
        Stream destination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(manifestBytes);
        ArgumentNullException.ThrowIfNull(destination);

        var root = Path.GetFullPath(contentRoot);
        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);

        WriteBytesEntry(archive, OpusPackageArchive.ManifestEntryName, manifestBytes);
        if (signatureBytes is not null)
        {
            WriteBytesEntry(archive, OpusPackageArchive.SignatureEntryName, signatureBytes);
        }

        foreach (var relativePath in OrderedPayloadPaths(manifest))
        {
            WriteFileEntry(archive, root, relativePath);
        }
    }

    private static List<string> OrderedPayloadPaths(ContentPackageManifest manifest)
    {
        var paths = new List<string>(manifest.Files.Count);
        foreach (var file in manifest.Files)
        {
            paths.Add(file.Path);
        }

        paths.Sort(StringComparer.Ordinal);
        return paths;
    }

    private static void WriteBytesEntry(ZipArchive archive, string entryName, byte[] bytes)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        entry.LastWriteTime = FixedEntryTimestamp;
        using var stream = entry.Open();
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void WriteFileEntry(ZipArchive archive, string root, string relativePath)
    {
        var physicalPath = Path.GetFullPath(
            Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var entry = archive.CreateEntry(relativePath, CompressionLevel.Optimal);
        entry.LastWriteTime = FixedEntryTimestamp;
        using var source = new FileStream(
            physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var entryStream = entry.Open();
        source.CopyTo(entryStream);
    }
}
