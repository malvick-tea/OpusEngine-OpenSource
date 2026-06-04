using System;
using System.IO;
using FluentAssertions;
using Opus.Content.Textures;
using Xunit;

namespace Opus.Content.Tests.Textures;

/// <summary>Specifies the disk cache + codec that make runtime BC compression a one-time cost. The
/// codec must round-trip a compressed texture byte-for-byte and reject a hash mismatch (re-authored
/// source) or foreign/truncated data; the cache must encode once then serve from disk, and re-encode
/// when the source content changes.</summary>
public sealed class CompressedTextureCacheTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"opus-bc-cache-{Guid.NewGuid():N}");

    public CompressedTextureCacheTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort temp cleanup.
        }
    }

    [Fact]
    public void Codec_round_trips_a_compressed_texture()
    {
        var texture = Sample();
        var hash = new byte[] { 1, 2, 3, 4 };

        var bytes = CompressedTextureCodec.Serialize(texture, hash);

        CompressedTextureCodec.TryDeserialize(bytes, hash, out var restored).Should().BeTrue();
        restored!.Width.Should().Be(8);
        restored.Height.Should().Be(8);
        restored.Format.Should().Be(BlockCompressionFormat.Bc7);
        restored.MipBlocks.Should().HaveCount(2);
        restored.MipBlocks[0].Should().Equal(texture.MipBlocks[0]);
        restored.MipBlocks[1].Should().Equal(texture.MipBlocks[1]);
    }

    [Fact]
    public void Codec_rejects_a_source_hash_mismatch()
    {
        var bytes = CompressedTextureCodec.Serialize(Sample(), new byte[] { 9, 9 });

        CompressedTextureCodec.TryDeserialize(bytes, new byte[] { 8, 8 }, out var restored).Should().BeFalse();
        restored.Should().BeNull();
    }

    [Fact]
    public void Codec_rejects_foreign_or_truncated_data()
    {
        CompressedTextureCodec.TryDeserialize(new byte[] { 1, 2, 3 }, Array.Empty<byte>(), out var restored).Should().BeFalse();
        restored.Should().BeNull();
    }

    [Fact]
    public void GetOrCreate_encodes_once_then_serves_from_the_cache()
    {
        var source = Path.Combine(_directory, "tower_basecolor.png");
        File.WriteAllBytes(source, new byte[] { 10, 20, 30, 40, 50 });
        var sourceBytes = File.ReadAllBytes(source);
        var encodeCalls = 0;

        var first = CompressedTextureCache.GetOrCreate(source, sourceBytes, () => Count(ref encodeCalls));
        File.Exists(source + CompressedTextureCache.CacheFileExtension).Should().BeTrue();
        var second = CompressedTextureCache.GetOrCreate(source, sourceBytes, () => Count(ref encodeCalls));

        encodeCalls.Should().Be(1, "the second call is served from the on-disk cache");
        second.MipBlocks[0].Should().Equal(first.MipBlocks[0]);
    }

    [Fact]
    public void GetOrCreate_re_encodes_when_the_source_content_changes()
    {
        var source = Path.Combine(_directory, "tower_basecolor.png");
        File.WriteAllBytes(source, new byte[] { 1 });
        var encodeCalls = 0;

        CompressedTextureCache.GetOrCreate(source, new byte[] { 1 }, () => Count(ref encodeCalls));
        CompressedTextureCache.GetOrCreate(source, new byte[] { 2 }, () => Count(ref encodeCalls));

        encodeCalls.Should().Be(2, "a different source hash invalidates the cached blob");
    }

    private static CompressedTexture Count(ref int calls)
    {
        calls++;
        return Sample();
    }

    private static CompressedTexture Sample()
    {
        var tail = new byte[16];
        for (var i = 0; i < tail.Length; i++)
        {
            tail[i] = (byte)(i + 1);
        }

        return new CompressedTexture(8, 8, BlockCompressionFormat.Bc7, new[] { new byte[16], tail });
    }
}
