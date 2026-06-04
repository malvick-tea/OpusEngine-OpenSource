using System;
using BCnEncoder.Encoder;
using BCnEncoder.Shared;

namespace Opus.Content.Textures;

/// <summary>Compresses decoded RGBA8 images into block-compressed GPU formats (BC7 / BC5) through
/// BCnEncoder.Net. The uncompressed loader uploads 4K PBR sets as raw RGBA8, which blows the VRAM
/// budget for a whole city's materials; block compression cuts that 4:1 while the maps stay directly
/// GPU-sampleable. A single image or a whole mip chain can be encoded — every level is compressed
/// independently from the engine's own box-filtered <see cref="MipChain"/>, so the minified BC levels
/// match the filter the uncompressed path already uses.</summary>
public static class BcnTextureEncoder
{
    /// <summary>Block-compresses one image and returns the tightly-packed BC block bytes (row-major
    /// 4×4 blocks) — the exact per-subresource layout <c>GetCopyableFootprints</c> reports, so the
    /// upload path needs no format-specific handling. Partial edge blocks on non-multiple-of-four
    /// dimensions are padded by the encoder, matching how the GPU stores the tail mip levels.</summary>
    public static byte[] Encode(DecodedImage image, BlockCompressionFormat format)
    {
        ArgumentNullException.ThrowIfNull(image);

        var encoder = new BcEncoder();
        encoder.OutputOptions.GenerateMipMaps = false;
        encoder.OutputOptions.Quality = CompressionQuality.Balanced;
        encoder.OutputOptions.Format = ToCompressionFormat(format);
        return encoder.EncodeToRawBytes(
            image.Rgba, image.Width, image.Height, PixelFormat.Rgba32, mipLevel: 0, out _, out _);
    }

    /// <summary>Box-filters <paramref name="baseLevel"/> into a full mip chain and block-compresses
    /// every level, returning a <see cref="CompressedTexture"/> ready for one mipped GPU upload.</summary>
    public static CompressedTexture EncodeMipChain(DecodedImage baseLevel, BlockCompressionFormat format)
    {
        ArgumentNullException.ThrowIfNull(baseLevel);

        var mipChain = MipChain.Generate(baseLevel);
        var blocks = new byte[mipChain.Count][];
        for (var level = 0; level < mipChain.Count; level++)
        {
            blocks[level] = Encode(mipChain[level], format);
        }

        return new CompressedTexture(baseLevel.Width, baseLevel.Height, format, blocks);
    }

    private static CompressionFormat ToCompressionFormat(BlockCompressionFormat format) => format switch
    {
        BlockCompressionFormat.Bc5 => CompressionFormat.Bc5,
        _ => CompressionFormat.Bc7,
    };
}
