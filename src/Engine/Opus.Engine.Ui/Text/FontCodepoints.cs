using System;
using System.Collections.Generic;
using System.Text;

namespace Opus.Engine.Ui.Text;

/// <summary>
/// Builds the codepoint set a font atlas must contain. Latin, Latin-1, common punctuation
/// and Cyrillic are fixed ranges — they fully cover the en/ru locales. CJK is open-ended
/// (20k+ ideographs), so instead of baking the whole block, the exact kana + kanji are
/// scanned from the localized text the build actually ships. The atlas then carries every
/// glyph the game can display and nothing more.
///
/// Backend-agnostic: lives in Engine.Ui so the Raylib and Direct3D12 draw-surface
/// backends share one codepoint-selection rule instead of each carrying a copy.
/// </summary>
public static class FontCodepoints
{
    private static readonly (int Start, int End)[] BaseRanges =
    {
        (0x0020, 0x007E),   // Basic Latin (printable ASCII)
        (0x00A0, 0x00FF),   // Latin-1 Supplement (incl. the degree sign)
        (0x0400, 0x04FF),   // Cyrillic
        (0x2010, 0x2027),   // General Punctuation (dashes, quotes, bullet, ellipsis)
    };

    // Symbols used by hardcoded, locale-independent chrome that the text scan would miss.
    private static readonly int[] ExtraSymbols = { 0x2605 };   // ★ — menu currency strip

    /// <summary>Returns the sorted, distinct codepoints for the atlas: the fixed base
    /// ranges plus every codepoint appearing in <paramref name="localizedText"/>.</summary>
    public static int[] ForLocalizedText(IEnumerable<string> localizedText) =>
        SortDistinct(CollectAllCodepoints(localizedText));

    /// <summary>Splits the same codepoint set into a Latin-band atlas (Latin + Latin-1 +
    /// General Punctuation + Cyrillic + extras) and a CJK-band atlas (Hiragana + Katakana
    /// + CJK Ideographs + Halfwidth / Fullwidth forms). Built so the bilingual draw
    /// surface can bake two narrow atlases — the Latin one with a UI-grade proportional
    /// font like Segoe UI, the CJK one with a wide Japanese face like Yu Gothic — and
    /// avoid Yu Gothic's fullwidth Cyrillic glyphs leaking into Russian text.</summary>
    public static FontCodepointBands ForLocalizedTextSplit(IEnumerable<string> localizedText)
    {
        var all = CollectAllCodepoints(localizedText);
        var latin = new HashSet<int>();
        var cjk = new HashSet<int>();
        foreach (var codepoint in all)
        {
            (IsCjkCodepoint(codepoint) ? cjk : latin).Add(codepoint);
        }

        return new FontCodepointBands(SortDistinct(latin), SortDistinct(cjk));
    }

    /// <summary>True when <paramref name="codepoint"/> lives in a Hiragana / Katakana /
    /// CJK Ideographs / Halfwidth-Fullwidth block — the ranges Yu Gothic is preferred
    /// for and Segoe UI does not cover.</summary>
    public static bool IsCjkCodepoint(int codepoint) =>
        (codepoint >= 0x3000 && codepoint <= 0x303F) ||
        (codepoint >= 0x3040 && codepoint <= 0x30FF) ||
        (codepoint >= 0x31F0 && codepoint <= 0x31FF) ||
        (codepoint >= 0x4E00 && codepoint <= 0x9FFF) ||
        (codepoint >= 0xFF00 && codepoint <= 0xFFEF);

    private static HashSet<int> CollectAllCodepoints(IEnumerable<string> localizedText)
    {
        var set = new HashSet<int>();
        foreach (var (start, end) in BaseRanges)
        {
            for (var codepoint = start; codepoint <= end; codepoint++)
            {
                set.Add(codepoint);
            }
        }

        foreach (var symbol in ExtraSymbols)
        {
            set.Add(symbol);
        }

        foreach (var text in localizedText)
        {
            foreach (var rune in text.EnumerateRunes())
            {
                set.Add(rune.Value);
            }
        }

        return set;
    }

    private static int[] SortDistinct(HashSet<int> set)
    {
        var result = new int[set.Count];
        set.CopyTo(result);
        Array.Sort(result);
        return result;
    }
}
