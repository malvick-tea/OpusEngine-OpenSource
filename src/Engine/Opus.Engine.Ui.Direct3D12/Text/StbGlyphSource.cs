using System;
using System.Runtime.InteropServices;
using StbTrueTypeSharp;
using static StbTrueTypeSharp.StbTrueType;

namespace Opus.Engine.Ui.Direct3D12.Text;

/// <summary>
/// One font face, opened through stb_truetype, that rasterises individual glyphs to
/// 8-bit coverage bitmaps. stb_truetype is the same rasteriser the Raylib backend uses
/// under the hood, so glyph shapes stay consistent between the two draw-surface backends.
/// </summary>
/// <remarks>
/// stb_truetype reads font tables straight out of the supplied byte buffer, so the buffer
/// is pinned for the lifetime of this source and released on <see cref="Dispose"/>. Only
/// standalone sfnt buffers are accepted — a <c>.ttc</c> member must already be lifted out
/// with <see cref="TrueTypeCollection"/>.
/// </remarks>
internal sealed unsafe class StbGlyphSource : IDisposable
{
    private readonly stbtt_fontinfo _font;
    private GCHandle _fontDataPin;
    private bool _disposed;

    private StbGlyphSource(stbtt_fontinfo font, GCHandle fontDataPin)
    {
        _font = font;
        _fontDataPin = fontDataPin;
    }

    /// <summary>Opens <paramref name="sfntBytes"/> as a font face. Returns <c>null</c>
    /// when the bytes are not a usable sfnt — the caller falls through to the next
    /// candidate face.</summary>
    public static StbGlyphSource? TryLoad(byte[] sfntBytes)
    {
        if (sfntBytes is null || sfntBytes.Length < 12)
        {
            return null;
        }

        var pin = GCHandle.Alloc(sfntBytes, GCHandleType.Pinned);
        try
        {
            var data = (byte*)pin.AddrOfPinnedObject();
            var offset = stbtt_GetFontOffsetForIndex(data, 0);
            var font = new stbtt_fontinfo();
            if (offset < 0 || stbtt_InitFont(font, data, offset) == 0)
            {
                pin.Free();
                return null;
            }

            return new StbGlyphSource(font, pin);
        }
        catch (Exception)
        {
            pin.Free();
            return null;
        }
    }

    /// <summary>True when this face actually carries a contour for the codepoint, so the
    /// baker can route a codepoint to the face that owns it.</summary>
    public bool HasGlyph(int codepoint) =>
        !_disposed && stbtt_FindGlyphIndex(_font, codepoint) != 0;

    /// <summary>Ascent / descent / line-gap in pixels at the given bake height. Ascent is
    /// positive (above the baseline), descent negative.</summary>
    public FontVerticalMetrics VerticalMetrics(float pixelHeight)
    {
        var scale = stbtt_ScaleForPixelHeight(_font, pixelHeight);
        int ascent;
        int descent;
        int lineGap;
        stbtt_GetFontVMetrics(_font, &ascent, &descent, &lineGap);
        return new FontVerticalMetrics(ascent * scale, descent * scale, lineGap * scale);
    }

    /// <summary>Rasterises one glyph at <paramref name="pixelHeight"/>. A glyph with no
    /// contour (a space) yields an empty coverage buffer but a real advance.</summary>
    public RasterizedGlyph Rasterize(int codepoint, float pixelHeight)
    {
        var scale = stbtt_ScaleForPixelHeight(_font, pixelHeight);
        int advance;
        int leftSideBearing;
        stbtt_GetCodepointHMetrics(_font, codepoint, &advance, &leftSideBearing);
        var advancePixels = advance * scale;

        int width;
        int height;
        int offsetX;
        int offsetY;
        var bitmap = stbtt_GetCodepointBitmap(_font, scale, scale, codepoint, &width, &height, &offsetX, &offsetY);
        if (bitmap == null || width <= 0 || height <= 0)
        {
            if (bitmap != null)
            {
                stbtt_FreeBitmap(bitmap, null);
            }

            return new RasterizedGlyph(Array.Empty<byte>(), 0, 0, 0, 0, advancePixels);
        }

        var coverage = new byte[width * height];
        new ReadOnlySpan<byte>(bitmap, coverage.Length).CopyTo(coverage);
        stbtt_FreeBitmap(bitmap, null);
        return new RasterizedGlyph(coverage, width, height, offsetX, offsetY, advancePixels);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_fontDataPin.IsAllocated)
        {
            _fontDataPin.Free();
        }
    }
}
