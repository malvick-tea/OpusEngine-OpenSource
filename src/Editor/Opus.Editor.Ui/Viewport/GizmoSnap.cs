using System;
using Opus.Editor.Core;

namespace Opus.Editor.Ui;

/// <summary>
/// Quantises a gizmo drag to a fixed grid when the user holds the snap modifier (Ctrl): a move snaps to whole
/// metres, a rotation to fixed-degree steps, and a scale to fixed factor steps. Scale snapping rounds the
/// magnitude with a one-step floor and keeps the sign, so it never collapses the node to zero and a deliberate
/// mirror (negative scale) stays mirrored. Pure helper shared by the controller's drag math.
/// </summary>
public static class GizmoSnap
{
    /// <summary>The world-space grid step a snapped translate lands on.</summary>
    public const float TranslateStepMeters = 1f;

    /// <summary>The angular step a snapped rotation lands on.</summary>
    public const float RotateStepDegrees = 15f;

    /// <summary>The factor step a snapped scale lands on.</summary>
    public const float ScaleStep = 0.25f;

    /// <summary>Rounds <paramref name="value"/> to the nearest multiple of <paramref name="step"/>.</summary>
    public static float ToStep(float value, float step) => MathF.Round(value / step) * step;

    /// <summary>Returns <paramref name="value"/> with only its <paramref name="axis"/> component snapped to the
    /// nearest multiple of <paramref name="step"/> — the other components (the axes the drag did not touch)
    /// keep their authored values.</summary>
    public static Float3 SnapAxis(Float3 value, GizmoAxis axis, float step) => axis switch
    {
        GizmoAxis.X => value with { X = ToStep(value.X, step) },
        GizmoAxis.Y => value with { Y = ToStep(value.Y, step) },
        GizmoAxis.Z => value with { Z = ToStep(value.Z, step) },
        _ => value,
    };

    /// <summary>Returns <paramref name="value"/> with only its <paramref name="axis"/> scale component snapped:
    /// the magnitude rounds to the nearest multiple of <paramref name="step"/> but never below one step, and
    /// the sign is preserved so a mirrored axis stays mirrored.</summary>
    public static Float3 SnapScaleAxis(Float3 value, GizmoAxis axis, float step) => axis switch
    {
        GizmoAxis.X => value with { X = SnapMagnitude(value.X, step) },
        GizmoAxis.Y => value with { Y = SnapMagnitude(value.Y, step) },
        GizmoAxis.Z => value with { Z = SnapMagnitude(value.Z, step) },
        _ => value,
    };

    private static float SnapMagnitude(float value, float step)
    {
        float magnitude = MathF.Max(step, ToStep(MathF.Abs(value), step));
        return value < 0f ? -magnitude : magnitude;
    }
}
