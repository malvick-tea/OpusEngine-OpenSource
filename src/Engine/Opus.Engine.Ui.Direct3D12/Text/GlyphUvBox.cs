using System.Runtime.InteropServices;

namespace Opus.Engine.Ui.Direct3D12.Text;

/// <summary>A glyph's rectangle inside the font atlas, in normalised 0..1 texture
/// coordinates. <c>(U0,V0)</c> is the top-left corner, <c>(U1,V1)</c> the bottom-right.</summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct GlyphUvBox(float U0, float V0, float U1, float V1);
