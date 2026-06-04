using System;
using System.Collections.Generic;
using System.Numerics;
using Opus.Engine.Ui.Text;

namespace Opus.Engine.Ui.Direct3D12.Text;

/// <summary>
/// Rasterises a codepoint set into a single coverage atlas. Each codepoint is routed to
/// the face that owns it — Japanese ranges to the CJK face, everything else to the Latin
/// face — and shelf-packed into one texture alongside a reserved opaque white texel that
/// lets the textured pixel-shader path also serve flat colour fills.
/// </summary>
internal static class GlyphAtlasBaker
{
    private const int WhiteBlockSize = 4;
    private const byte OpaqueCoverage = 255;

    /// <summary>Bakes <paramref name="codepoints"/> at <paramref name="pixelHeight"/> into
    /// a <paramref name="atlasWidth"/>×<paramref name="atlasHeight"/> coverage atlas.
    /// Throws <see cref="GlyphAtlasOverflowException"/> when the glyphs do not fit.</summary>
    public static BakedGlyphAtlas Bake(
        int[] codepoints,
        StbGlyphSource latin,
        StbGlyphSource cjk,
        float pixelHeight,
        int atlasWidth,
        int atlasHeight)
    {
        var packer = new GlyphAtlasPacker(atlasWidth, atlasHeight);
        var coverage = new byte[atlasWidth * atlasHeight];
        var glyphs = new Dictionary<int, GlyphMetric>(codepoints.Length);

        var whiteUv = ReserveWhiteBlock(packer, coverage, atlasWidth, atlasHeight, pixelHeight, codepoints.Length);

        foreach (var codepoint in codepoints)
        {
            var raster = SelectSource(codepoint, latin, cjk).Rasterize(codepoint, pixelHeight);
            glyphs[codepoint] = raster.HasRaster
                ? PlaceGlyph(raster, packer, coverage, atlasWidth, atlasHeight, pixelHeight, codepoints.Length)
                : new GlyphMetric(default, 0, 0, 0, 0, raster.Advance);
        }

        var vertical = latin.VerticalMetrics(pixelHeight);
        var lineHeight = vertical.Ascent - vertical.Descent + vertical.LineGap;
        return new BakedGlyphAtlas(
            coverage, atlasWidth, atlasHeight, whiteUv, glyphs, pixelHeight, vertical.Ascent, lineHeight);
    }

    /// <summary>Routes a codepoint to the face that carries it: a Japanese-range codepoint
    /// to the CJK face when that face owns the glyph, otherwise the Latin face.</summary>
    private static StbGlyphSource SelectSource(int codepoint, StbGlyphSource latin, StbGlyphSource cjk) =>
        FontCodepoints.IsCjkCodepoint(codepoint) && cjk.HasGlyph(codepoint) ? cjk : latin;

    private static GlyphMetric PlaceGlyph(
        in RasterizedGlyph raster,
        GlyphAtlasPacker packer,
        byte[] coverage,
        int atlasWidth,
        int atlasHeight,
        float pixelHeight,
        int glyphCount)
    {
        if (!packer.TryPack(raster.Width, raster.Height, out var x, out var y))
        {
            throw new GlyphAtlasOverflowException(glyphCount, atlasWidth, atlasHeight, pixelHeight);
        }

        Blit(coverage, atlasWidth, raster, x, y);
        var uv = new GlyphUvBox(
            x / (float)atlasWidth,
            y / (float)atlasHeight,
            (x + raster.Width) / (float)atlasWidth,
            (y + raster.Height) / (float)atlasHeight);
        return new GlyphMetric(uv, raster.Width, raster.Height, raster.OffsetX, raster.OffsetY, raster.Advance);
    }

    private static Vector2 ReserveWhiteBlock(
        GlyphAtlasPacker packer, byte[] coverage, int atlasWidth, int atlasHeight, float pixelHeight, int glyphCount)
    {
        if (!packer.TryPack(WhiteBlockSize, WhiteBlockSize, out var x, out var y))
        {
            throw new GlyphAtlasOverflowException(glyphCount, atlasWidth, atlasHeight, pixelHeight);
        }

        for (var row = 0; row < WhiteBlockSize; row++)
        {
            for (var col = 0; col < WhiteBlockSize; col++)
            {
                coverage[((y + row) * atlasWidth) + x + col] = OpaqueCoverage;
            }
        }

        // The block centre — safely inside the opaque region for any sampler footprint.
        return new Vector2(
            (x + (WhiteBlockSize * 0.5f)) / atlasWidth,
            (y + (WhiteBlockSize * 0.5f)) / atlasHeight);
    }

    private static void Blit(byte[] coverage, int atlasWidth, in RasterizedGlyph raster, int x, int y)
    {
        for (var row = 0; row < raster.Height; row++)
        {
            var source = row * raster.Width;
            var destination = ((y + row) * atlasWidth) + x;
            raster.Coverage.AsSpan(source, raster.Width).CopyTo(coverage.AsSpan(destination, raster.Width));
        }
    }
}
