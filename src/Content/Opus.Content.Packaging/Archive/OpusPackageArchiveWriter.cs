using System.Buffers;
using System.IO.Compression;
using System.Security.Cryptography;
using Opus.Content.Packaging.Manifest;
using Opus.Foundation.IO;

namespace Opus.Content.Packaging.Archive;

/// <summary>
/// Writes a <c>.opkg</c> archive: the reserved manifest (and optional signature) entries plus
/// every manifest-declared payload file, added in a fixed order with a fixed timestamp so
/// repacking identical content yields a stable artifact. The caller has already validated the
/// manifest paths and the size budget. Every payload is streamed once into the archive while
/// its exact bytes are hashed and checked against the signed manifest, preventing the source
/// tree from changing between manifest generation and archive creation.
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

        foreach (var file in OrderedPayloadFiles(manifest))
        {
            WriteFileEntry(archive, root, file);
        }
    }

    private static List<ContentPackageFile> OrderedPayloadFiles(ContentPackageManifest manifest)
    {
        var files = new List<ContentPackageFile>(manifest.Files);
        files.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.Path, right.Path));
        return files;
    }

    private static void WriteBytesEntry(ZipArchive archive, string entryName, byte[] bytes)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        entry.LastWriteTime = FixedEntryTimestamp;
        using var stream = entry.Open();
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void WriteFileEntry(ZipArchive archive, string root, ContentPackageFile file)
    {
        var relativePath = file.Path.Replace('/', Path.DirectorySeparatorChar);
        var physicalPath = PathContainment.ResolveUnderRoot(root, relativePath);
        PathContainment.RejectReparsePoints(root, physicalPath);

        var entry = archive.CreateEntry(file.Path, CompressionLevel.Optimal);
        entry.LastWriteTime = FixedEntryTimestamp;
        using var source = new FileStream(
            physicalPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            FileOptions.SequentialScan);
        using var entryStream = entry.Open();
        CopyAndVerify(source, entryStream, file);
    }

    private static void CopyAndVerify(Stream source, Stream destination, ContentPackageFile file)
    {
        byte[] expectedHash;
        try
        {
            expectedHash = Convert.FromHexString(file.Sha256);
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException(
                $"Manifest SHA-256 for '{file.Path}' is not valid hexadecimal.",
                ex);
        }

        if (expectedHash.Length != SHA256.HashSizeInBytes)
        {
            throw new InvalidDataException(
                $"Manifest SHA-256 for '{file.Path}' is not {SHA256.HashSizeInBytes} bytes.");
        }

        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        long bytesWritten = 0;
        try
        {
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                bytesWritten = checked(bytesWritten + read);
                hasher.AppendData(buffer, 0, read);
                destination.Write(buffer, 0, read);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
            ArrayPool<byte>.Shared.Return(buffer);
        }

        Span<byte> actualHash = stackalloc byte[SHA256.HashSizeInBytes];
        if (!hasher.TryGetHashAndReset(actualHash, out var written)
            || written != SHA256.HashSizeInBytes)
        {
            throw new InvalidOperationException("SHA-256 produced an unexpected digest length.");
        }

        if (bytesWritten != file.SizeBytes)
        {
            throw new InvalidDataException(
                $"Content size for '{file.Path}' changed: manifest declares {file.SizeBytes} bytes, read {bytesWritten} bytes.");
        }

        if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
        {
            throw new InvalidDataException(
                $"Content hash for '{file.Path}' does not match the signed manifest.");
        }
    }
}
