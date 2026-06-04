namespace Opus.Engine.Ui.Text;

/// <summary>Codepoint partition for a bilingual atlas. <see cref="Latin"/> covers UI
/// chrome, en / ru text and General Punctuation; <see cref="Cjk"/> covers the Japanese
/// glyphs a wide face such as Yu Gothic / MS Gothic carries.</summary>
public sealed record FontCodepointBands(int[] Latin, int[] Cjk);
