using System;
using System.Numerics;
using Opus.Engine.Ui;

namespace Opus.Engine.Ui.Direct3D12.Batching;

/// <summary>
/// Pure expansion of an <see cref="InkStroke"/> polyline into <see cref="UiQuadBatch"/> quads: one
/// swept quad per segment (the thick-line sweep) plus one analytic filled disc at every point so
/// caps and interior joins are round and gap-free at any bend angle. Reuses
/// <see cref="UiQuadGeometry"/> — ink introduces no new draw mode or shader. A single-point stroke
/// is one disc (a dot); an empty or zero-width stroke emits nothing.
/// </summary>
internal static class UiInkTessellator
{
    public static void Append(UiQuadBatch batch, InkStroke stroke, Vector2 whiteUv)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(stroke);

        var points = stroke.Points;
        if (points.Count == 0 || stroke.WidthPx <= 0f)
        {
            return;
        }

        // Swept rectangle per segment.
        for (var i = 0; i + 1 < points.Count; i++)
        {
            UiQuadGeometry.Line(
                batch, points[i].X, points[i].Y, points[i + 1].X, points[i + 1].Y,
                stroke.WidthPx, stroke.Color, whiteUv);
        }

        // Round cap / join disc per point — closes the wedge gaps the rectangular segments leave at
        // bends and rounds both ends. A single-point stroke is just this one disc.
        var radius = stroke.WidthPx * 0.5f;
        for (var i = 0; i < points.Count; i++)
        {
            UiQuadGeometry.Circle(batch, points[i].X, points[i].Y, radius, stroke.Color);
        }
    }
}
