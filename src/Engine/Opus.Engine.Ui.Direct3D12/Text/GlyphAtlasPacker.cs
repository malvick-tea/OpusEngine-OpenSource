using System;

namespace Opus.Engine.Ui.Direct3D12.Text;

/// <summary>
/// Shelf-row rectangle packer for the glyph atlas. Glyphs are baked in roughly ascending
/// size order, so a single-pass shelf packer wastes little space while staying trivially
/// cheap — no need for a full guillotine / max-rects packer at UI-atlas scale.
/// </summary>
/// <remarks>
/// Every placed rectangle is surrounded by a one-texel gap so bilinear sampling of one
/// glyph never bleeds a neighbour's coverage in.
/// </remarks>
internal sealed class GlyphAtlasPacker
{
    private const int Padding = 1;

    private readonly int _width;
    private readonly int _height;
    private int _shelfX;
    private int _shelfY;
    private int _shelfHeight;

    public GlyphAtlasPacker(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Atlas dimensions must be positive.");
        }

        _width = width;
        _height = height;
        _shelfX = Padding;
        _shelfY = Padding;
    }

    /// <summary>Reserves a <paramref name="width"/>×<paramref name="height"/> cell and
    /// returns its top-left texel. Returns <c>false</c> when the cell cannot fit even on a
    /// fresh shelf — the caller treats that as atlas overflow.</summary>
    public bool TryPack(int width, int height, out int x, out int y)
    {
        x = 0;
        y = 0;
        if (width <= 0 || height <= 0 || width + (2 * Padding) > _width)
        {
            return false;
        }

        if (_shelfX + width + Padding > _width)
        {
            _shelfX = Padding;
            _shelfY += _shelfHeight + Padding;
            _shelfHeight = 0;
        }

        if (_shelfY + height + Padding > _height)
        {
            return false;
        }

        x = _shelfX;
        y = _shelfY;
        _shelfX += width + Padding;
        _shelfHeight = Math.Max(_shelfHeight, height);
        return true;
    }
}
