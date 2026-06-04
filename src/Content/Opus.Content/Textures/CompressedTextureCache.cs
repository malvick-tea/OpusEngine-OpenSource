using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.Cryptography;

namespace Opus.Content.Textures;

/// <summary>Persistent on-disk cache for block-compressed textures, keyed by the source image's
/// content hash. BC7-encoding a 4K map costs seconds; caching the cooked blob next to the source
/// turns a city's material-set compression into a one-time cost — every later launch loads the
/// cached result and skips the encoder, the "cook on first run" pattern shipped engines use for
/// shader and texture caches. A re-authored source (different SHA-256), a format change, or a codec
/// version bump misses the cache and re-encodes. Cache writes are best-effort: a read-only content
/// directory degrades to encode-every-launch rather than failing the load.</summary>
public static class CompressedTextureCache
{
    /// <summary>Extension appended to the source image path to name its cooked cache sibling.</summary>
    public const string CacheFileExtension = ".obc";

    /// <summary>Returns the cached compression of <paramref name="sourcePath"/> if a valid sibling
    /// cache file matches the current source bytes, otherwise invokes <paramref name="encode"/>,
    /// writes the result to the cache (best-effort), and returns it.</summary>
    public static CompressedTexture GetOrCreate(
        string sourcePath,
        ReadOnlySpan<byte> sourceBytes,
        Func<CompressedTexture> encode)
    {
        ArgumentNullException.ThrowIfNull(sourcePath);
        ArgumentNullException.ThrowIfNull(encode);

        var cachePath = sourcePath + CacheFileExtension;
        var sourceHash = SHA256.HashData(sourceBytes);

        if (TryLoad(cachePath, sourceHash, out var cached))
        {
            return cached;
        }

        var encoded = encode();
        TrySave(cachePath, encoded, sourceHash);
        return encoded;
    }

    private static bool TryLoad(string cachePath, ReadOnlySpan<byte> sourceHash, [NotNullWhen(true)] out CompressedTexture? texture)
    {
        texture = null;
        if (!File.Exists(cachePath))
        {
            return false;
        }

        try
        {
            return CompressedTextureCodec.TryDeserialize(File.ReadAllBytes(cachePath), sourceHash, out texture);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void TrySave(string cachePath, CompressedTexture texture, ReadOnlySpan<byte> sourceHash)
    {
        try
        {
            File.WriteAllBytes(cachePath, CompressedTextureCodec.Serialize(texture, sourceHash));
        }
        catch (IOException)
        {
            // Best-effort: a read-only or full content directory just re-encodes next launch.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
