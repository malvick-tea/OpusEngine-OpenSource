using System;
using System.Collections.Generic;
using System.Numerics;

namespace Opus.Editor.Ui;

/// <summary>A viewport line projected to window pixels: both endpoints in screen space, plus its role.</summary>
/// <param name="A">First endpoint in window pixels.</param>
/// <param name="B">Second endpoint in window pixels.</param>
/// <param name="Role">Visual role, mapped to a colour by the render seam.</param>
public readonly record struct ProjectedLine(Vector2 A, Vector2 B, ViewportLineRole Role);

/// <summary>
/// Projects the world-space <see cref="ViewportSceneDrawList"/> into window-pixel line segments inside a
/// viewport rectangle, using the orbit camera's row-vector view-projection (the engine convention, matching
/// <see cref="WorldScreenProjector"/>). A line is dropped when either endpoint is on or behind the camera
/// plane, so a partially clipped segment never streaks across the screen. Pure — the D3D12 seam draws the
/// result with the existing UI line batch (one render path, ADR-0028 / ADR-0033).
/// </summary>
public static class EditorViewportProjection
{
    public static IReadOnlyList<ProjectedLine> Project(
        OrbitCamera camera, EditorPanelRect viewport, IReadOnlyList<ViewportLine> worldLines)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(worldLines);

        var viewProjection = camera.ViewMatrix * camera.ProjectionMatrix(viewport.AspectRatio);
        var origin = new Vector2(viewport.X, viewport.Y);
        var projected = new List<ProjectedLine>(worldLines.Count);
        foreach (var line in worldLines)
        {
            if (WorldScreenProjector.TryProject(line.A, viewProjection, viewport.Width, viewport.Height, out var a)
                && WorldScreenProjector.TryProject(line.B, viewProjection, viewport.Width, viewport.Height, out var b))
            {
                projected.Add(new ProjectedLine(a + origin, b + origin, line.Role));
            }
        }

        return projected;
    }
}
