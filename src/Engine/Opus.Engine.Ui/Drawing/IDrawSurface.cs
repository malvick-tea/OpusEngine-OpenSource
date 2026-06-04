namespace Opus.Engine.Ui;

/// <summary>
/// Immediate-mode draw surface. Screens take this in their <see cref="IScreen.Render"/>
/// method and call back into it. Coordinates are in pixels relative to the top-left of
/// the surface (y grows downward).
///
/// Per layout v2 §7 this lives in Engine.Ui (interface) with concrete impls per backend
/// (Engine.Ui.Raylib for M2; Vulkan/Metal land later). Frame-graph + Renderer take over
/// once we leave the immediate-mode era around M3.
/// </summary>
public interface IDrawSurface
{
    int Width { get; }

    int Height { get; }

    void Clear(Color color);

    void FillRect(int x, int y, int w, int h, Color color);

    void StrokeRect(int x, int y, int w, int h, int thickness, Color color);

    /// <summary>Draws a 1+ pixel thick line from (x0,y0) to (x1,y1). Backends use native primitives where available.</summary>
    void DrawLine(int x0, int y0, int x1, int y1, int thickness, Color color);

    /// <summary>Filled circle centred at (cx, cy) with the given radius.</summary>
    void FillCircle(int cx, int cy, int radius, Color color);

    /// <summary>1+ pixel thick circle outline centred at (cx, cy).</summary>
    void StrokeCircle(int cx, int cy, int radius, int thickness, Color color);

    void DrawText(string text, int x, int y, int fontSize, Color color);

    int MeasureText(string text, int fontSize);
}
