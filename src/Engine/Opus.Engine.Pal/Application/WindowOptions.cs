namespace Opus.Engine.Pal.Application;

/// <summary>
/// Immutable creation parameters for a host window. Concrete <see cref="IWindowService"/>
/// implementations may clamp values (e.g. mobile platforms ignore <see cref="Width"/> /
/// <see cref="Height"/>) but never mutate the descriptor itself.
/// </summary>
public readonly record struct WindowOptions(
    string Title,
    int Width,
    int Height,
    bool Resizable,
    bool VSync,
    WindowMode Mode)
{
    /// <summary>Default 1280×720 windowed, vsync on, resizable. Suitable for early dev.</summary>
    public static WindowOptions Default(string title) =>
        new(title, 1280, 720, Resizable: true, VSync: true, WindowMode.Windowed);
}

public enum WindowMode
{
    Windowed,
    BorderlessFullscreen,
    ExclusiveFullscreen,
}
