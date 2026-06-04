using System;

namespace Opus.Engine.Ui.Text;

/// <summary>
/// Where a world-space label lands on the 2D draw surface after projection. Produced by
/// <see cref="WorldSpaceTextProjector"/>; consumed by the caller to position an ordinary
/// <see cref="IDrawSurface.DrawText"/> call. Screen coordinates are surface pixels (top-left
/// origin, y down), matching every other UI primitive.
/// </summary>
/// <param name="Visible">False when the world point is at or behind the camera, or outside the
/// view volume — the caller draws nothing.</param>
/// <param name="ScreenX">Projected x in surface pixels.</param>
/// <param name="ScreenY">Projected y in surface pixels.</param>
/// <param name="NormalizedDepth">Clip-space depth in <c>[0,1]</c> (D3D convention); useful for
/// depth-sorting overlapping labels or scaling font size with distance.</param>
public readonly record struct WorldSpaceTextAnchor(bool Visible, float ScreenX, float ScreenY, float NormalizedDepth)
{
    /// <summary>The shared not-visible result (point culled). Carries no screen position.</summary>
    public static WorldSpaceTextAnchor Hidden { get; } = new(false, 0f, 0f, 0f);

    /// <summary>Projected x rounded to the nearest pixel.</summary>
    public int PixelX => (int)MathF.Round(ScreenX);

    /// <summary>Projected y rounded to the nearest pixel.</summary>
    public int PixelY => (int)MathF.Round(ScreenY);

    /// <summary>Left-edge x for a label of <paramref name="measuredWidth"/> pixels centred
    /// horizontally on the anchor — pair with <see cref="IDrawSurface.MeasureText"/>.</summary>
    public int CenteredLeft(int measuredWidth) => (int)MathF.Round(ScreenX - (measuredWidth * 0.5f));
}
