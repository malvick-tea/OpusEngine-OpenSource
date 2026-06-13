using System;
using System.Collections.Generic;
using System.Numerics;
using Opus.Editor.Core;

namespace Opus.Editor.Ui;

/// <summary>One world-space segment of a rotate-gizmo ring: the axis it rotates about and the segment's two
/// endpoints. The composer turns it into a <see cref="ViewportLine"/> and the controller projects it for
/// hit-testing, so both the draw and the pick share one ring geometry.</summary>
/// <param name="Axis">Which world axis this ring rotates about.</param>
/// <param name="A">First world-space endpoint.</param>
/// <param name="B">Second world-space endpoint.</param>
public readonly record struct RotateGizmoSegment(GizmoAxis Axis, Vector3 A, Vector3 B);

/// <summary>
/// The world-space geometry and math of the rotate gizmo: three rings, one per axis, each a closed loop in
/// the plane perpendicular to its axis, plus the pure ray-to-angle solve the controller turns into a
/// rotation during a drag. Dragging a ring rotates the node about that world axis, changing only the matching
/// Euler component (X about X, Y about Y, Z about Z) — exactly parallel to <see cref="TranslateGizmo"/>
/// (position) and <see cref="ScaleGizmo"/> (scale). Pure — the composer tessellates the rings into
/// <see cref="ViewportLine"/>s through the existing line batch (one render path, ADR-0028 / ADR-0033); no new
/// shader, PSO, or pass.
/// </summary>
public static class RotateGizmo
{
    /// <summary>How many segments approximate each ring loop: enough that the projected circle reads smooth,
    /// cheap in the existing line batch.</summary>
    public const int RingSegmentCount = 48;

    /// <summary>Below this absolute dot of the pick ray with the ring's axis the ray is nearly parallel to the
    /// rotation plane (the ring is edge-on), so the angle solve is ill-conditioned and the drag holds.</summary>
    public const float MinPlaneDot = 1e-3f;

    private const float RadiansToDegrees = 180f / MathF.PI;

    private static readonly GizmoAxis[] AxisOrder = { GizmoAxis.X, GizmoAxis.Y, GizmoAxis.Z };

    /// <summary>The three rings' segments at <paramref name="origin"/>, each ring of radius
    /// <paramref name="radius"/> in the plane perpendicular to its axis. Consecutive segments share an
    /// endpoint and the loop is closed.</summary>
    public static IReadOnlyList<RotateGizmoSegment> Segments(Vector3 origin, float radius)
    {
        var segments = new List<RotateGizmoSegment>(AxisOrder.Length * RingSegmentCount);
        foreach (var axis in AxisOrder)
        {
            var previous = RingPoint(axis, origin, radius, 0f);
            for (int step = 1; step <= RingSegmentCount; step++)
            {
                float angle = step / (float)RingSegmentCount * MathF.Tau;
                var current = RingPoint(axis, origin, radius, angle);
                segments.Add(new RotateGizmoSegment(axis, previous, current));
                previous = current;
            }
        }

        return segments;
    }

    /// <summary>A point on the <paramref name="axis"/> ring at <paramref name="angleRadians"/> around the
    /// <paramref name="origin"/>, in the plane perpendicular to that axis.</summary>
    public static Vector3 RingPoint(GizmoAxis axis, Vector3 origin, float radius, float angleRadians)
    {
        var (u, v) = PlaneBasis(axis);
        return origin + (radius * ((u * MathF.Cos(angleRadians)) + (v * MathF.Sin(angleRadians))));
    }

    /// <summary>Appends the three rings as viewport lines, colouring the active (dragged) axis with the
    /// highlight role and the others with their per-axis role (shared with the move / scale gizmos).</summary>
    public static void AppendDrawLines(
        ICollection<ViewportLine> sink, Vector3 origin, float radius, GizmoAxis activeAxis)
    {
        ArgumentNullException.ThrowIfNull(sink);
        foreach (var segment in Segments(origin, radius))
        {
            sink.Add(new ViewportLine(segment.A, segment.B, GizmoAxisRoles.For(segment.Axis, activeAxis)));
        }
    }

    /// <summary>Resolves the angle of the pick <paramref name="ray"/> around the <paramref name="axis"/> ring:
    /// intersects the ray with the rotation plane (through <paramref name="origin"/>, normal = the axis) and
    /// measures the hit point's angle in that plane. Taking the difference between this at grab time and now
    /// gives the rotation swept. False when the ray is nearly parallel to the plane (the ring is edge-on) or
    /// the plane lies behind the camera, so the caller holds the last value.</summary>
    public static bool TryAngle(Ray ray, Vector3 origin, GizmoAxis axis, out float angleRadians)
    {
        angleRadians = 0f;
        var normal = TranslateGizmo.AxisUnit(axis);
        float facing = Vector3.Dot(ray.Direction, normal);
        if (MathF.Abs(facing) < MinPlaneDot)
        {
            return false;
        }

        float distance = Vector3.Dot(origin - ray.Origin, normal) / facing;
        if (distance <= 0f)
        {
            return false;
        }

        var inPlane = ray.At(distance) - origin;
        var (u, v) = PlaneBasis(axis);
        angleRadians = MathF.Atan2(Vector3.Dot(inPlane, v), Vector3.Dot(inPlane, u));
        return true;
    }

    /// <summary>Returns <paramref name="startEuler"/> with only the dragged <paramref name="axis"/> component
    /// advanced by <paramref name="deltaDegrees"/>.</summary>
    public static Float3 Rotate(Float3 startEuler, GizmoAxis axis, float deltaDegrees) => axis switch
    {
        GizmoAxis.X => startEuler with { X = startEuler.X + deltaDegrees },
        GizmoAxis.Y => startEuler with { Y = startEuler.Y + deltaDegrees },
        GizmoAxis.Z => startEuler with { Z = startEuler.Z + deltaDegrees },
        _ => startEuler,
    };

    /// <summary>The signed degrees swept from <paramref name="grabAngleRadians"/> to
    /// <paramref name="currentAngleRadians"/>, wrapped to (-180, 180] so dragging across the ring's seam does
    /// not jump a full turn. Past ±180 the Euler value wraps to the equivalent orientation, not a glitch.</summary>
    public static float DeltaDegrees(float grabAngleRadians, float currentAngleRadians) =>
        WrapSignedDegrees((currentAngleRadians - grabAngleRadians) * RadiansToDegrees);

    /// <summary>Wraps degrees to the half-open range (-180, 180].</summary>
    public static float WrapSignedDegrees(float degrees)
    {
        float wrapped = degrees % 360f;
        if (wrapped <= -180f)
        {
            return wrapped + 360f;
        }

        return wrapped > 180f ? wrapped - 360f : wrapped;
    }

    // The two orthonormal in-plane basis vectors for an axis ring (the plane perpendicular to the axis),
    // chosen so U x V = the axis, giving a consistent right-handed sense to the swept angle.
    private static (Vector3 U, Vector3 V) PlaneBasis(GizmoAxis axis) => axis switch
    {
        GizmoAxis.X => (Vector3.UnitY, Vector3.UnitZ),
        GizmoAxis.Y => (Vector3.UnitZ, Vector3.UnitX),
        _ => (Vector3.UnitX, Vector3.UnitY),
    };
}
