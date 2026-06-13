using System.Collections.Generic;
using Opus.Engine.Ui;

namespace Opus.Editor.Ui.Tests;

/// <summary>An <see cref="IDrawSurface"/> that records the calls made to it, so the editor frame drawer is
/// verified headlessly without a GPU.</summary>
internal sealed class RecordingDrawSurface : IDrawSurface
{
    public RecordingDrawSurface(int width = 1280, int height = 720)
    {
        Width = width;
        Height = height;
    }

    public int Width { get; }

    public int Height { get; }

    public int ClearCount { get; private set; }

    public List<(int X, int Y, int W, int H, Color Color)> Fills { get; } = new();

    public List<(int X0, int Y0, int X1, int Y1, int Thickness, Color Color)> Lines { get; } = new();

    public List<(string Text, int X, int Y, int Size)> Texts { get; } = new();

    public void Clear(Color color) => ClearCount++;

    public void FillRect(int x, int y, int w, int h, Color color) => Fills.Add((x, y, w, h, color));

    public void StrokeRect(int x, int y, int w, int h, int thickness, Color color)
    {
    }

    public void DrawLine(int x0, int y0, int x1, int y1, int thickness, Color color) =>
        Lines.Add((x0, y0, x1, y1, thickness, color));

    public void FillCircle(int cx, int cy, int radius, Color color)
    {
    }

    public void StrokeCircle(int cx, int cy, int radius, int thickness, Color color)
    {
    }

    public void DrawText(string text, int x, int y, int fontSize, Color color) => Texts.Add((text, x, y, fontSize));

    public int MeasureText(string text, int fontSize) => text.Length * (fontSize / 2);
}
