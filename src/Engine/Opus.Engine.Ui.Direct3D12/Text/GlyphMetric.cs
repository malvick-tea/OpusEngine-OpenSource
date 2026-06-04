using System.Runtime.InteropServices;

namespace Opus.Engine.Ui.Direct3D12.Text;

/// <summary>
/// Placement and spacing data for one baked glyph, measured at the atlas's bake pixel
/// height. Layout scales every field by <c>requestedFontSize / bakePixelHeight</c>, so
/// the atlas is rasterised once and reused across font sizes.
/// </summary>
/// <param name="Uv">The glyph's box in the atlas. Meaningless when the glyph has no
/// raster footprint (a space) — <see cref="PixelWidth"/> is then zero.</param>
/// <param name="PixelWidth">Rasterised bitmap width at bake height.</param>
/// <param name="PixelHeight">Rasterised bitmap height at bake height.</param>
/// <param name="BearingX">Horizontal offset from the pen origin to the bitmap's left edge.</param>
/// <param name="BearingY">Vertical offset from the text baseline to the bitmap's top edge
/// (negative — the bitmap top sits above the baseline).</param>
/// <param name="Advance">Horizontal pen advance after drawing this glyph.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct GlyphMetric(
    GlyphUvBox Uv,
    int PixelWidth,
    int PixelHeight,
    float BearingX,
    float BearingY,
    float Advance)
{
    /// <summary>True when the glyph carries pixels to draw (a space / control glyph does not).</summary>
    public bool HasRaster => PixelWidth > 0 && PixelHeight > 0;
}
