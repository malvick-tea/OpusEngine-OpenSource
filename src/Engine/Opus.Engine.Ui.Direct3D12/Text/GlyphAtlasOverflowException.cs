using System;
using System.Diagnostics.CodeAnalysis;

namespace Opus.Engine.Ui.Direct3D12.Text;

/// <summary>
/// Thrown when the requested glyph set does not fit the atlas texture at the chosen bake
/// height. Raised at the asset boundary (atlas construction) so a misconfigured locale /
/// bake-size combination surfaces as a clear failure instead of silently dropping glyphs.
/// </summary>
[SuppressMessage("Design", "RCS1194:Implement exception constructors", Justification = "Atlas-overflow only arises from this assembly's bake path with concrete glyph-count / atlas-size diagnostics — the standard parameterless / message-only ctors would lose that context and never be called.")]
public sealed class GlyphAtlasOverflowException : Exception
{
    public GlyphAtlasOverflowException(int glyphCount, int atlasWidth, int atlasHeight, float bakePixelHeight)
        : base($"Glyph set of {glyphCount} codepoints does not fit a {atlasWidth}x{atlasHeight} atlas "
            + $"at a {bakePixelHeight:0.#}px bake height. Raise the atlas size or lower the bake height.")
    {
        GlyphCount = glyphCount;
        AtlasWidth = atlasWidth;
        AtlasHeight = atlasHeight;
        BakePixelHeight = bakePixelHeight;
    }

    public int GlyphCount { get; }

    public int AtlasWidth { get; }

    public int AtlasHeight { get; }

    public float BakePixelHeight { get; }
}
