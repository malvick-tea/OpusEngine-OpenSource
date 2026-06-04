namespace Opus.Engine.Ui;

/// <summary>
/// Linear-space sRGB colour with 8-bit channels. Independent of any graphics library —
/// the renderer backend is responsible for translating into its native colour type
/// (Raylib_cs.Color, Vulkan VkClearValue, etc.).
/// </summary>
public readonly record struct Color(byte R, byte G, byte B, byte A)
{
    public static Color FromRgb(byte r, byte g, byte b) => new(r, g, b, 255);

    public static Color FromRgba(byte r, byte g, byte b, byte a) => new(r, g, b, a);

    public static Color WithAlpha(Color c, byte a) => new(c.R, c.G, c.B, a);

    public static readonly Color Transparent = new(0, 0, 0, 0);

    public static readonly Color Black = new(0, 0, 0, 255);

    public static readonly Color White = new(255, 255, 255, 255);
}
