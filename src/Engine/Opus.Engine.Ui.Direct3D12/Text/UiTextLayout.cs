using System;
using Opus.Engine.Ui;
using Opus.Engine.Ui.Direct3D12.Batching;

namespace Opus.Engine.Ui.Direct3D12.Text;

/// <summary>
/// Pure CPU layout from a string to a sequence of glyph quads. Walks runes left to right,
/// looks up each codepoint's metric in a <see cref="BakedGlyphAtlas"/>, scales pen + bearing
/// + dimensions by <c>requestedSize / bakePixelHeight</c>, and appends one textured quad
/// per glyph to a <see cref="UiQuadBatch"/>. Codepoints absent from the atlas are skipped
/// silently — the atlas was built from the localized text set, so an unknown rune is a
/// real bug at content-lint time rather than something to paint.
/// <para>
/// GPU-free by design: the layout consumes the same baked metrics that
/// <see cref="D3D12FontAtlas"/> uploads, so tests drive it without a graphics device.
/// </para>
/// </summary>
internal static class UiTextLayout
{
    /// <summary>Appends the glyph run for <paramref name="text"/> to <paramref name="batch"/>
    /// and returns the rendered width in pixels (final pen position minus the start).</summary>
    public static float Append(
        UiQuadBatch batch, string text, int x, int y, int fontSize, Color color, BakedGlyphAtlas atlas)
    {
        var scale = fontSize / atlas.BakePixelHeight;
        var baseline = y + (atlas.Ascent * scale);
        var penX = (float)x;
        foreach (var rune in text.EnumerateRunes())
        {
            if (!atlas.TryGetGlyph(rune.Value, out var metric))
            {
                continue;
            }

            if (metric.HasRaster)
            {
                var dx = penX + (metric.BearingX * scale);
                var dy = baseline + (metric.BearingY * scale);
                UiQuadGeometry.Glyph(batch, dx, dy, metric.PixelWidth * scale, metric.PixelHeight * scale, metric.Uv, color);
            }

            penX += metric.Advance * scale;
        }

        return penX - x;
    }

    /// <summary>Reports the rendered width <paramref name="text"/> would occupy at
    /// <paramref name="fontSize"/> without producing geometry — for layout / measurement.</summary>
    public static int Measure(string text, int fontSize, BakedGlyphAtlas atlas)
    {
        var scale = fontSize / atlas.BakePixelHeight;
        var pen = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            if (atlas.TryGetGlyph(rune.Value, out var metric))
            {
                pen += metric.Advance * scale;
            }
        }

        return (int)MathF.Ceiling(pen);
    }
}
