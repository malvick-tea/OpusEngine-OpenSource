using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Opus.Content.Textures;

/// <summary>Binary (de)serialiser for a cached block-compressed texture — the on-disk artefact the
/// <see cref="CompressedTextureCache"/> writes so a slow BC7 encode of a 4K map runs once and every
/// later load reads the cooked result. The body embeds a content hash of the SOURCE image, so a
/// re-authored texture invalidates its own cache automatically. Layout: ASCII magic <c>OBC1</c>, a
/// version byte, the compression format, width, height, a length-prefixed source hash, then a
/// length-prefixed BC blob per mip level (level 0 first). Deserialisation is exhaustively validated
/// because the input is an untrusted file: any malformed field fails closed to a re-encode.</summary>
public static class CompressedTextureCodec
{
    private const byte Version = 1;

    // Sanity ceilings so a corrupt header claiming a gigantic length fails closed instead of
    // attempting a huge allocation. 16 mip levels covers up to a 32K base; 64 MiB per level covers
    // an 8K BC blob with room to spare.
    private const int MaxMipLevels = 16;
    private const int MaxBlockBytes = 64 * 1024 * 1024;
    private const int MaxHashBytes = 64;
    private const int MaxDimension = 32768;
    private const int MaxEncodedCacheBytes = MaxBlockBytes + 4096;

    private static readonly byte[] Magic = "OBC1"u8.ToArray();

    public static byte[] Serialize(CompressedTexture texture, ReadOnlySpan<byte> sourceHash)
    {
        ArgumentNullException.ThrowIfNull(texture);

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Magic);
            writer.Write(Version);
            writer.Write((byte)texture.Format);
            writer.Write(texture.Width);
            writer.Write(texture.Height);
            writer.Write(sourceHash.Length);
            writer.Write(sourceHash);
            writer.Write(texture.MipBlocks.Count);
            foreach (var block in texture.MipBlocks)
            {
                writer.Write(block.Length);
                writer.Write(block);
            }
        }

        return stream.ToArray();
    }

    /// <summary>Reads a cache blob and validates it against the expected source hash. Returns
    /// <see langword="false"/> — leaving <paramref name="texture"/> null — on a wrong magic/version,
    /// an unknown format, a hash mismatch (the source was re-authored), or any truncation.</summary>
    public static bool TryDeserialize(
        ReadOnlySpan<byte> data,
        ReadOnlySpan<byte> expectedSourceHash,
        out CompressedTexture? texture)
    {
        texture = null;
        if (data.Length > MaxEncodedCacheBytes)
        {
            return false;
        }

        try
        {
            using var stream = new MemoryStream(data.ToArray(), writable: false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);

            if (!reader.ReadBytes(Magic.Length).AsSpan().SequenceEqual(Magic) || reader.ReadByte() != Version)
            {
                return false;
            }

            var formatByte = reader.ReadByte();
            if (!Enum.IsDefined((BlockCompressionFormat)formatByte))
            {
                return false;
            }

            var width = reader.ReadInt32();
            var height = reader.ReadInt32();
            if (width is <= 0 or > MaxDimension || height is <= 0 or > MaxDimension)
            {
                return false;
            }

            var hashLength = reader.ReadInt32();
            if (hashLength is < 0 or > MaxHashBytes)
            {
                return false;
            }

            if (!ReadExactly(reader, hashLength, out var hash)
                || !CryptographicOperations.FixedTimeEquals(expectedSourceHash, hash))
            {
                return false;
            }

            var mipCount = reader.ReadInt32();
            if (mipCount is <= 0 or > MaxMipLevels)
            {
                return false;
            }

            var blocks = new byte[mipCount][];
            long totalBlockBytes = 0;
            for (var level = 0; level < mipCount; level++)
            {
                var length = reader.ReadInt32();
                var expectedLength = ExpectedBlockBytes(width, height, level);
                if (length != expectedLength
                    || length > MaxBlockBytes
                    || (totalBlockBytes += length) > MaxBlockBytes
                    || !ReadExactly(reader, length, out blocks[level]))
                {
                    return false;
                }
            }

            if (stream.Position != stream.Length)
            {
                return false;
            }

            texture = new CompressedTexture(width, height, (BlockCompressionFormat)formatByte, blocks);
            return true;
        }
        catch (EndOfStreamException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static bool ReadExactly(BinaryReader reader, int count, out byte[] bytes)
    {
        bytes = reader.ReadBytes(count);
        return bytes.Length == count;
    }

    private static int ExpectedBlockBytes(int width, int height, int mipLevel)
    {
        var mipWidth = Math.Max(1, width >> mipLevel);
        var mipHeight = Math.Max(1, height >> mipLevel);
        var blockColumns = (mipWidth + 3L) / 4L;
        var blockRows = (mipHeight + 3L) / 4L;
        var byteCount = blockColumns * blockRows * 16L;
        return byteCount > int.MaxValue ? -1 : (int)byteCount;
    }
}
