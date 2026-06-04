using BCnEncoder.Decoder;
using BCnEncoder.Shared;
using FluentAssertions;
using Opus.Content.Textures;
using Xunit;

namespace Opus.Content.Tests.Textures;

/// <summary>Specifies <see cref="BcnTextureEncoder"/> — the BC7 / BC5 block-compression wrapper that
/// shrinks 4K PBR maps 4:1 before upload. The block-count cases pin the tightly-packed layout the
/// GPU upload path relies on (16 bytes per 4×4 block); the round-trip decodes the BC7 output back
/// through BCnEncoder's decoder to prove the channel order + dimensions survive (a width/height swap
/// or BGRA mix-up would corrupt the recovered colour).</summary>
public sealed class BcnTextureEncoderTests
{
    // BC5 and BC7 both pack a 4×4 texel block into 128 bits.
    private const int BlockBytes = 16;

    [Theory]
    [InlineData(4, 4, 1)]
    [InlineData(8, 8, 4)]
    [InlineData(8, 4, 2)]
    [InlineData(16, 16, 16)]
    public void Encode_bc7_packs_one_block_per_four_by_four_tile(int width, int height, int blocks)
    {
        var encoded = BcnTextureEncoder.Encode(Solid(width, height, 180, 90, 40), BlockCompressionFormat.Bc7);

        encoded.Should().HaveCount(blocks * BlockBytes);
    }

    [Fact]
    public void Encode_bc5_uses_the_same_sixteen_byte_block_as_bc7()
    {
        BcnTextureEncoder.Encode(Solid(8, 8, 128, 200, 255), BlockCompressionFormat.Bc5)
            .Should().HaveCount(4 * BlockBytes);
    }

    [Fact]
    public void Encode_bc7_round_trips_a_solid_colour_within_tolerance()
    {
        var encoded = BcnTextureEncoder.Encode(Solid(8, 8, 200, 120, 60), BlockCompressionFormat.Bc7);

        var decoded = new BcDecoder().DecodeRaw(encoded, 8, 8, CompressionFormat.Bc7);

        var texel = decoded[(4 * 8) + 4];
        ((int)texel.r).Should().BeCloseTo(200, 8);
        ((int)texel.g).Should().BeCloseTo(120, 8);
        ((int)texel.b).Should().BeCloseTo(60, 8);
    }

    [Fact]
    public void EncodeMipChain_compresses_every_box_filtered_level_down_to_one_by_one()
    {
        var compressed = BcnTextureEncoder.EncodeMipChain(Solid(8, 8, 100, 150, 200), BlockCompressionFormat.Bc7);

        compressed.Width.Should().Be(8);
        compressed.Height.Should().Be(8);
        compressed.Format.Should().Be(BlockCompressionFormat.Bc7);
        compressed.MipBlocks.Should().HaveCount(4, "8 → 4 → 2 → 1");
        compressed.MipBlocks[0].Should().HaveCount(4 * BlockBytes);
        compressed.MipBlocks[^1].Should().HaveCount(BlockBytes, "the 1×1 tail is one padded block");
    }

    private static DecodedImage Solid(int width, int height, byte r, byte g, byte b)
    {
        var rgba = new byte[width * height * 4];
        for (var i = 0; i < rgba.Length; i += 4)
        {
            rgba[i] = r;
            rgba[i + 1] = g;
            rgba[i + 2] = b;
            rgba[i + 3] = byte.MaxValue;
        }

        return new DecodedImage(width, height, rgba);
    }
}
