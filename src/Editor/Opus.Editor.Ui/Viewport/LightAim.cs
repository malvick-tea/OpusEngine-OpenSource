using System;
using System.Numerics;
using Opus.Editor.Core;

namespace Opus.Editor.Ui;

/// <summary>
/// Rotates a light's aim direction about a world gizmo axis — the rotate-ring drag for a directional or
/// spot light. A direction is a vector, not an Euler triple, so the ring's swept angle applies as an
/// axis-angle rotation of the vector rather than a per-channel Euler edit. Pure.
/// </summary>
public static class LightAim
{
    private const float DegreesToRadians = MathF.PI / 180f;

    /// <summary>Returns <paramref name="direction"/> rotated <paramref name="deltaDegrees"/> about the
    /// world axis of <paramref name="axis"/>. A zero direction is returned unchanged (there is nothing to
    /// aim).</summary>
    public static Float3 Rotate(Float3 direction, GizmoAxis axis, float deltaDegrees)
    {
        var vector = direction.ToVector3();
        if (vector.LengthSquared() <= 0f)
        {
            return direction;
        }

        var rotation = Quaternion.CreateFromAxisAngle(TranslateGizmo.AxisUnit(axis), deltaDegrees * DegreesToRadians);
        return Float3.FromVector3(Vector3.Transform(vector, rotation));
    }
}
