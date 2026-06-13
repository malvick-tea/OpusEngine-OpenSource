namespace Opus.Engine.Ui.Direct3D12.Text;

/// <summary>
/// Ordered candidate font files for the bilingual glyph atlas. The first file that
/// exists and opens wins. The split mirrors <c>Engine.Ui.Raylib</c>'s bilingual atlas:
/// a tight proportional face carries Latin + Cyrillic, a wide Japanese face carries the
/// kana + kanji that face does not cover.
/// </summary>
/// <remarks>
/// These are deployment facts about the host OS, not tunable values — they live here as
/// named constants rather than scattered string literals. Since ADR-0034 the Latin / Cyrillic
/// band bakes from the bundled Roboto face (<see cref="BundledFonts"/>) for deterministic
/// cross-machine rendering; this <see cref="Latin"/> list is now only the fallback the atlas
/// reaches for if the embedded Roboto resource is ever stripped from the binary. The
/// <see cref="Cjk"/> list is still the primary CJK source.
/// </remarks>
internal static class FontFaceCandidates
{
    /// <summary>Latin / Latin-1 / Cyrillic / punctuation faces — Segoe UI is the narrow
    /// Windows UI face; the rest are fallbacks for non-Windows dev hosts.</summary>
    public static readonly string[] Latin =
    {
        @"C:\Windows\Fonts\segoeui.ttf",
        @"C:\Windows\Fonts\arial.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
    };

    /// <summary>Hiragana / Katakana / CJK-ideograph faces. All Windows Japanese system
    /// fonts ship as TrueType Collections, hence the <c>.ttc</c> extensions.</summary>
    public static readonly string[] Cjk =
    {
        @"C:\Windows\Fonts\YuGothR.ttc",
        @"C:\Windows\Fonts\YuGothM.ttc",
        @"C:\Windows\Fonts\meiryo.ttc",
        @"C:\Windows\Fonts\msgothic.ttc",
    };
}
