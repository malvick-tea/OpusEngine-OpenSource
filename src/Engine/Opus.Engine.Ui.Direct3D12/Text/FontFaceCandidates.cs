namespace Opus.Engine.Ui.Direct3D12.Text;

/// <summary>
/// Ordered candidate font files for the bilingual glyph atlas. The first file that
/// exists and opens wins. The split mirrors <c>Engine.Ui.Raylib</c>'s bilingual atlas:
/// a tight proportional face carries Latin + Cyrillic, a wide Japanese face carries the
/// kana + kanji that face does not cover.
/// </summary>
/// <remarks>
/// These are deployment facts about the host OS, not tunable values. They live here as`r`n/// named constants rather than scattered string literals. The Latin / Cyrillic band first`r`n/// tries an optional embedded face (<see cref="BundledFonts"/>), then this <see cref="Latin"/>`r`n/// list. The <see cref="Cjk"/> list is the primary CJK source.`r`n/// </remarks>
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
