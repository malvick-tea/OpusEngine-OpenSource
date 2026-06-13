namespace Opus.Editor.Ui;

/// <summary>
/// An axis-aligned pixel rectangle of the editor window's 2D chrome. Top-left origin, y grows downward, to
/// match <see cref="Opus.Engine.Ui.IDrawSurface"/>. Pure value type.
/// </summary>
/// <param name="X">Left edge in pixels.</param>
/// <param name="Y">Top edge in pixels.</param>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
public readonly record struct EditorPanelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    /// <summary>The rectangle's aspect ratio (width / height), used to build the viewport projection. Falls
    /// back to 1 for a degenerate height so callers never divide by zero.</summary>
    public float AspectRatio => Height <= 0 ? 1f : (float)Width / Height;

    /// <summary>True when the pixel lies inside the rectangle (right / bottom edges exclusive).</summary>
    public bool Contains(int px, int py) => px >= X && px < Right && py >= Y && py < Bottom;

    /// <summary>Maps a pixel to this rectangle's normalised [0,1] space (0,0 = top-left, 1,1 = bottom-right).
    /// The host uses it to turn a viewport-relative mouse position into a pick ray. Values fall outside
    /// [0,1] for pixels outside the rectangle; callers gate on <see cref="Contains"/> when that matters.</summary>
    public (float X01, float Y01) Normalize(int px, int py) =>
        (Width <= 0 ? 0f : (px - X) / (float)Width, Height <= 0 ? 0f : (py - Y) / (float)Height);
}

/// <summary>
/// The pixel layout of the editor window's 2D chrome for a given window size: a top toolbar, the 3D
/// <see cref="Viewport"/> on the left, a right-hand column split into the scene <see cref="Outliner"/>
/// (top), the selection properties <see cref="Inspector"/> (middle), and the live pseudo-code mirror
/// (<see cref="DslPanel"/>, below), and a bottom status bar. Produced by <see cref="EditorChromeLayout"/>;
/// the D3D12 seam fills these rectangles and draws the projected scene inside <see cref="Viewport"/>.
/// </summary>
/// <param name="Toolbar">Full-width strip across the top.</param>
/// <param name="Viewport">The 3D scene area the camera renders into.</param>
/// <param name="Outliner">Right-hand panel listing the scene's nodes for selection.</param>
/// <param name="Inspector">Right-hand panel showing (and editing) the selected element's properties.</param>
/// <param name="DslPanel">Right-hand panel showing the scene's live pseudo-code mirror.</param>
/// <param name="StatusBar">Full-width strip across the bottom.</param>
public readonly record struct EditorChrome(
    EditorPanelRect Toolbar,
    EditorPanelRect Viewport,
    EditorPanelRect Outliner,
    EditorPanelRect Inspector,
    EditorPanelRect DslPanel,
    EditorPanelRect StatusBar);
