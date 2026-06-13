using System;
using System.Collections.Generic;
using System.Numerics;

namespace Opus.Editor.Ui;

/// <summary>A translate-gizmo handle projected to viewport pixels: the axis it drives and its screen segment.</summary>
/// <param name="Axis">Which axis this handle drives.</param>
/// <param name="A">The gizmo origin in viewport pixels.</param>
/// <param name="B">The handle tip in viewport pixels.</param>
public readonly record struct GizmoScreenHandle(GizmoAxis Axis, Vector2 A, Vector2 B);

/// <summary>
/// Picks the translate-gizmo axis under a viewport click by 2D distance to each projected handle segment:
/// the nearest handle within <see cref="PickPixelRadius"/> wins, else <see cref="GizmoAxis.None"/>. Pure —
/// the controller projects the world handles to screen and feeds them here.
/// </summary>
public static class GizmoAxisPicker
{
    /// <summary>How close (in viewport pixels) a click must be to a handle to grab its axis.</summary>
    public const float PickPixelRadius = 10f;

    private const float MinSegmentLengthSquared = 1e-6f;

    public static GizmoAxis Pick(Vector2 clickPixels, IReadOnlyList<GizmoScreenHandle> handles)
    {
        ArgumentNullException.ThrowIfNull(handles);
        var picked = GizmoAxis.None;
        float nearest = PickPixelRadius;
        foreach (var handle in handles)
        {
            float distance = DistanceToSegment(clickPixels, handle.A, handle.B);
            if (distance <= nearest)
            {
                nearest = distance;
                picked = handle.Axis;
            }
        }

        return picked;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        var segment = b - a;
        float lengthSquared = segment.LengthSquared();
        if (lengthSquared < MinSegmentLengthSquared)
        {
            return Vector2.Distance(point, a);
        }

        float t = Math.Clamp(Vector2.Dot(point - a, segment) / lengthSquared, 0f, 1f);
        return Vector2.Distance(point, a + (segment * t));
    }
}
