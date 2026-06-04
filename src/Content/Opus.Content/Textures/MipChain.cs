using System;
using System.Collections.Generic;

namespace Opus.Content.Textures;

/// <summary>Generates a 2D mipmap chain from a decoded base image by repeated 2×2
/// box-filter downsampling — each level halves both dimensions (floored, never below 1)
/// down to a 1×1 level. The renderer uploads every level so a trilinear / anisotropic
/// sampler has the minified detail it needs; without a mip chain a high-resolution camo
/// texture viewed small aliases into shimmering noise and reads as a washed-out flat
/// surface.
/// <para>
/// Pure CPU — no GPU, no platform dependency — so texture-quality regression tests run
/// headless. Channels are averaged in the stored 8-bit space: the engine samples these
/// textures as linear unorm, so a plain per-channel average is consistent with how the
/// base level itself is read.
/// </para></summary>
public static class MipChain
{
    private const int ChannelCount = 4;

    /// <summary>Mip-level count for a <paramref name="width"/>×<paramref name="height"/>
    /// texture: <c>floor(log2(max(width, height))) + 1</c>.</summary>
    public static int LevelCount(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Texture dimensions must be positive.");
        }

        var largest = Math.Max(width, height);
        var levels = 1;
        while (largest > 1)
        {
            largest >>= 1;
            levels++;
        }

        return levels;
    }

    /// <summary>Builds the full mip chain. Index 0 is <paramref name="baseLevel"/> itself
    /// (returned by reference — callers must treat the result as immutable); indices
    /// 1..N−1 are the successive 2×2 box-filtered downsamples.</summary>
    public static IReadOnlyList<DecodedImage> Generate(DecodedImage baseLevel)
    {
        ArgumentNullException.ThrowIfNull(baseLevel);

        var levels = new List<DecodedImage>(LevelCount(baseLevel.Width, baseLevel.Height)) { baseLevel };
        var current = baseLevel;
        while (current.Width > 1 || current.Height > 1)
        {
            current = Downsample(current);
            levels.Add(current);
        }

        return levels;
    }

    /// <summary>One 2×2 box-filter downsample step. Each destination texel averages the
    /// four source texels beneath it; an odd source dimension clamps the trailing sample
    /// so the edge row / column is folded in rather than dropped.</summary>
    private static DecodedImage Downsample(DecodedImage source)
    {
        var dstWidth = Math.Max(1, source.Width / 2);
        var dstHeight = Math.Max(1, source.Height / 2);
        var dst = new byte[dstWidth * dstHeight * ChannelCount];
        var src = source.Rgba;
        var srcWidth = source.Width;

        for (var y = 0; y < dstHeight; y++)
        {
            var sy0 = y * 2;
            var sy1 = Math.Min(sy0 + 1, source.Height - 1);
            for (var x = 0; x < dstWidth; x++)
            {
                var sx0 = x * 2;
                var sx1 = Math.Min(sx0 + 1, srcWidth - 1);
                var topLeft = ((sy0 * srcWidth) + sx0) * ChannelCount;
                var topRight = ((sy0 * srcWidth) + sx1) * ChannelCount;
                var bottomLeft = ((sy1 * srcWidth) + sx0) * ChannelCount;
                var bottomRight = ((sy1 * srcWidth) + sx1) * ChannelCount;
                var destPixel = ((y * dstWidth) + x) * ChannelCount;
                for (var channel = 0; channel < ChannelCount; channel++)
                {
                    var sum = src[topLeft + channel] + src[topRight + channel]
                            + src[bottomLeft + channel] + src[bottomRight + channel];
                    dst[destPixel + channel] = (byte)((sum + 2) / 4);
                }
            }
        }

        return new DecodedImage(dstWidth, dstHeight, dst);
    }
}
