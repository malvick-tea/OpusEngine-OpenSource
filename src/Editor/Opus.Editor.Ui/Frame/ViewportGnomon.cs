using System;
using System.Collections.Generic;
using System.Numerics;

namespace Opus.Editor.Ui;

/// <summary>
/// The viewport's corner orientation indicator: three short axis stubs anchored in the viewport's
/// bottom-left corner, pointing where the world X / Y / Z axes currently project on screen — the standard
/// "which way am I facing" affordance after orbiting. Each stub keeps its true foreshortening (an axis
/// pointing at the camera draws short), so the gnomon reads like a miniature of the world axes. Pure —
/// the lines join the frame's projected line set and draw through the existing batch with the shared
/// gizmo axis colours (no new render concept).
/// </summary>
public static class ViewportGnomon
{
    /// <summary>The stub length in pixels at full foreshortening.</summary>
    public const float ArmLengthPixels = 28f;

    /// <summary>The anchor inset from the viewport's bottom-left corner, in pixels.</summary>
    public const float CornerInsetPixels = 44f;

    public static IReadOnlyList<ProjectedLine> Build(OrbitCamera camera, EditorPanelRect viewport)
    {
        ArgumentNullException.ThrowIfNull(camera);
        var view = camera.ViewMatrix;
        var anchor = new Vector2(viewport.X + CornerInsetPixels, viewport.Bottom - CornerInsetPixels);
        return new[]
        {
            Arm(anchor, view, Vector3.UnitX, ViewportLineRole.GizmoX),
            Arm(anchor, view, Vector3.UnitY, ViewportLineRole.GizmoY),
            Arm(anchor, view, Vector3.UnitZ, ViewportLineRole.GizmoZ),
        };
    }

    private static ProjectedLine Arm(Vector2 anchor, Matrix4x4 view, Vector3 worldAxis, ViewportLineRole role)
    {
        // Rotating the axis direction into view space gives its screen direction directly: view X runs
        // right, view Y runs up (screen Y runs down), and the Z component is the foreshortening the
        // 2D length loses naturally.
        var viewDirection = Vector3.TransformNormal(worldAxis, view);
        var tip = anchor + (new Vector2(viewDirection.X, -viewDirection.Y) * ArmLengthPixels);
        return new ProjectedLine(anchor, tip, role);
    }
}
