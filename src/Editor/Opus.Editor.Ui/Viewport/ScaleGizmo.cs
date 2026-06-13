using System;
using System.Collections.Generic;
using System.Numerics;
using Opus.Editor.Core;

namespace Opus.Editor.Ui;

/// <summary>
/// The world-space geometry and math of the scale gizmo: the same three axis handles as the translate gizmo
/// (so picking and the axis-parameter solve are shared), each capped with a small cube at its tip so a scale
/// handle reads distinctly from a plain move handle, plus the pure per-axis scale-factor math the controller
/// applies during a drag. Pure — the composer turns the handles into <see cref="ViewportLine"/>s through the
/// existing line batch (one render path, ADR-0028 / ADR-0033); no new shader, PSO, or pass.
/// </summary>
public static class ScaleGizmo
{
    /// <summary>The tip cube's half-extent as a fraction of the handle length, so it stays a small,
    /// constant-looking knob at any zoom.</summary>
    public const float TipCubeHalfExtentFactor = 0.08f;

    /// <summary>Below this absolute grab parameter the scale ratio is unstable (the grab happened almost on
    /// the gizmo origin), so a drag holds rather than dividing by a near-zero value.</summary>
    public const float MinGrabParameter = 1e-3f;

    // The 12 edges of a cube, as index pairs into the 8 corners produced by AppendCube (corner index bits
    // are x,y,z); each edge joins two corners that differ in exactly one axis.
    private static readonly (int A, int B)[] CubeEdges =
    {
        (0, 1), (2, 3), (4, 5), (6, 7),
        (0, 2), (1, 3), (4, 6), (5, 7),
        (0, 4), (1, 5), (2, 6), (3, 7),
    };

    /// <summary>Returns <paramref name="startScale"/> with only the dragged <paramref name="axis"/> component
    /// multiplied by <paramref name="factor"/>.</summary>
    public static Float3 Scale(Float3 startScale, GizmoAxis axis, float factor) => axis switch
    {
        GizmoAxis.X => startScale with { X = startScale.X * factor },
        GizmoAxis.Y => startScale with { Y = startScale.Y * factor },
        GizmoAxis.Z => startScale with { Z = startScale.Z * factor },
        _ => startScale,
    };

    /// <summary>The scale factor for a drag: the ratio of the current axis parameter to the grab parameter,
    /// so grabbing a handle and dragging outward grows the node and inward shrinks it (past the origin
    /// mirrors). False when the grab happened too near the gizmo origin for the ratio to be stable.</summary>
    public static bool TryFactor(float grabParameter, float currentParameter, out float factor)
    {
        if (MathF.Abs(grabParameter) < MinGrabParameter)
        {
            factor = 1f;
            return false;
        }

        factor = currentParameter / grabParameter;
        return true;
    }

    /// <summary>Appends the three axis handles plus a tip cube per axis as viewport lines, colouring the
    /// active (dragged) axis with the highlight role and the others with their per-axis role.</summary>
    public static void AppendDrawLines(
        ICollection<ViewportLine> sink, Vector3 origin, float length, GizmoAxis activeAxis)
    {
        ArgumentNullException.ThrowIfNull(sink);
        float cubeHalfExtent = length * TipCubeHalfExtentFactor;
        foreach (var handle in TranslateGizmo.Handles(origin, length))
        {
            var role = GizmoAxisRoles.For(handle.Axis, activeAxis);
            sink.Add(new ViewportLine(handle.Start, handle.End, role));
            AppendCube(sink, handle.End, cubeHalfExtent, role);
        }
    }

    private static void AppendCube(
        ICollection<ViewportLine> sink, Vector3 center, float halfExtent, ViewportLineRole role)
    {
        Span<Vector3> corners = stackalloc Vector3[8];
        int index = 0;
        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int sy = -1; sy <= 1; sy += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    corners[index++] = center + new Vector3(sx * halfExtent, sy * halfExtent, sz * halfExtent);
                }
            }
        }

        foreach (var (a, b) in CubeEdges)
        {
            sink.Add(new ViewportLine(corners[a], corners[b], role));
        }
    }
}
