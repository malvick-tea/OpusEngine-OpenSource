namespace Opus.Engine.Ui.Direct3D12.Text;

/// <summary>
/// One glyph rasterised by <see cref="StbGlyphSource"/>: an 8-bit coverage bitmap plus
/// the placement metrics stb_truetype reports. A blank glyph (space) carries an empty
/// <see cref="Coverage"/> and zero dimensions but still a non-zero <see cref="Advance"/>.
/// </summary>
/// <param name="Coverage">Row-major, top-down 8-bit alpha coverage, <see cref="Width"/>×
/// <see cref="Height"/> bytes.</param>
/// <param name="Width">Bitmap width in texels.</param>
/// <param name="Height">Bitmap height in texels.</param>
/// <param name="OffsetX">Pen-origin-relative x of the bitmap's left edge.</param>
/// <param name="OffsetY">Baseline-relative y of the bitmap's top edge (negative — above
/// the baseline).</param>
/// <param name="Advance">Horizontal pen advance in pixels.</param>
internal readonly record struct RasterizedGlyph(
    byte[] Coverage,
    int Width,
    int Height,
    int OffsetX,
    int OffsetY,
    float Advance)
{
    public bool HasRaster => Width > 0 && Height > 0;
}
