using System;
using System.Collections.Generic;
using System.Numerics;

namespace Opus.Engine.Ui.Direct3D12.Text;

/// <summary>
/// CPU-side result of baking a glyph set into one texture: the single-channel coverage
/// bitmap plus per-glyph placement metrics. <see cref="GlyphAtlasBaker"/> produces it;
/// <see cref="D3D12FontAtlas"/> uploads the bitmap to the GPU and <see cref="UiTextLayout"/>
/// reads the metrics. GPU-free and immutable, so it is straightforward to assert against.
/// </summary>
internal sealed class BakedGlyphAtlas
{
    private readonly IReadOnlyDictionary<int, GlyphMetric> _glyphs;

    public BakedGlyphAtlas(
        byte[] coverage,
        int width,
        int height,
        Vector2 whiteUv,
        IReadOnlyDictionary<int, GlyphMetric> glyphs,
        float bakePixelHeight,
        float ascent,
        float lineHeight)
    {
        Coverage = coverage;
        Width = width;
        Height = height;
        WhiteUv = whiteUv;
        _glyphs = glyphs;
        BakePixelHeight = bakePixelHeight;
        Ascent = ascent;
        LineHeight = lineHeight;
    }

    /// <summary>Single-channel (R8) coverage, row-major, <see cref="Width"/>×<see cref="Height"/> bytes.</summary>
    public byte[] Coverage { get; }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Atlas coordinate of the fully-opaque white texel. Solid-fill quads sample
    /// here so the textured pixel-shader path doubles as a flat colour fill.</summary>
    public Vector2 WhiteUv { get; }

    /// <summary>Pixel height the glyphs were rasterised at. Layout scales metrics by
    /// <c>requestedFontSize / BakePixelHeight</c>.</summary>
    public float BakePixelHeight { get; }

    /// <summary>Distance from the text baseline up to the top of the tallest glyph, in
    /// bake-height pixels.</summary>
    public float Ascent { get; }

    /// <summary>Baseline-to-baseline line spacing in bake-height pixels.</summary>
    public float LineHeight { get; }

    public int GlyphCount => _glyphs.Count;

    /// <summary>Looks up a glyph's metrics; <c>false</c> when the codepoint was not baked.</summary>
    public bool TryGetGlyph(int codepoint, out GlyphMetric metric) =>
        _glyphs.TryGetValue(codepoint, out metric);
}
