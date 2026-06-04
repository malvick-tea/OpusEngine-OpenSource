using System.Security.Cryptography;

namespace Opus.Content.Packaging.Validation;

/// <summary>Hash helpers for package manifest integrity checks. The streaming overloads
/// avoid loading the whole file into memory for large alpha assets — the in-memory
/// overload is retained for small inputs the validator already buffered for type-specific
/// inspection (texture decode, glTF binary read).</summary>
public static class PackageFileHash
{
    private const int StreamBufferSize = 81920;

    /// <summary>Computes a lower-case SHA-256 hex digest from an in-memory byte span.</summary>
    public static string ComputeSha256Hex(ReadOnlySpan<byte> bytes)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(bytes, hash);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Computes a lower-case SHA-256 hex digest by streaming <paramref name="stream"/>
    /// through <see cref="IncrementalHash"/>. Use this for files that may exceed the safe
    /// in-memory budget — peak memory stays at the buffer (80 KiB), not the file size.</summary>
    public static string ComputeSha256Hex(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(StreamBufferSize);
        try
        {
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                hasher.AppendData(buffer, 0, read);
            }
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
        }

        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        if (!hasher.TryGetHashAndReset(hash, out var written) || written != SHA256.HashSizeInBytes)
        {
            throw new InvalidOperationException("SHA-256 produced an unexpected digest length.");
        }

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Streams <paramref name="stream"/> through SHA-256 while enforcing an upper bound
    /// on the number of bytes read. Returns <see langword="false"/> as soon as the stream yields
    /// more than <paramref name="maxBytes"/> bytes — a lying archive header or a zip-bomb — so a
    /// caller never decompresses past its budget. On success <paramref name="hex"/> is the
    /// lower-case digest and <paramref name="byteCount"/> the exact number of bytes read.</summary>
    public static bool TryComputeSha256Hex(Stream stream, long maxBytes, out string hex, out long byteCount)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (maxBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBytes), maxBytes, "Byte budget must be non-negative.");
        }

        hex = string.Empty;
        byteCount = 0;
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(StreamBufferSize);
        try
        {
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                byteCount += read;
                if (byteCount > maxBytes)
                {
                    return false;
                }

                hasher.AppendData(buffer, 0, read);
            }
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
        }

        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        if (!hasher.TryGetHashAndReset(hash, out var written) || written != SHA256.HashSizeInBytes)
        {
            throw new InvalidOperationException("SHA-256 produced an unexpected digest length.");
        }

        hex = Convert.ToHexString(hash).ToLowerInvariant();
        return true;
    }

    /// <summary>Convenience overload: opens <paramref name="path"/> for sequential read and
    /// streams it through SHA-256. The file handle is opened with
    /// <see cref="FileShare.Read"/> so concurrent validator invocations on shared CI
    /// content do not collide.</summary>
    public static string ComputeSha256HexFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: StreamBufferSize,
            FileOptions.SequentialScan);
        return ComputeSha256Hex(stream);
    }
}
