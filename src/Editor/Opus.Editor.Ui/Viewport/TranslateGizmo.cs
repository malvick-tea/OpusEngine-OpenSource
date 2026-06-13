using System.Collections.Generic;
using System.Numerics;
using Opus.Editor.Core;

namespace Opus.Editor.Ui;

/// <summary>Which translate-gizmo axis a pointer is interacting with (none when off every handle).</summary>
public enum GizmoAxis
{
    /// <summary>No axis (the pointer is not on a handle).</summary>
    None,

    /// <summary>The world X axis.</summary>
    X,

    /// <summary>The world Y axis.</summary>
    Y,

    /// <summary>The world Z axis.</summary>
    Z,
}

/// <summary>One world-space translate-gizmo handle: an axis and the segment drawn for it (origin → tip).</summary>
/// <param name="Axis">Which world axis this handle drives.</param>
/// <param name="Start">The gizmo origin (the selected node's position).</param>
/// <param name="End">The handle tip, one handle-length along the axis.</param>
public readonly record struct GizmoHandle(GizmoAxis Axis, Vector3 Start, Vector3 End);

/// <summary>
/// The world-space geometry of the translate gizmo drawn on the selected node: three axis handles whose
/// length scales with the camera distance so the gizmo keeps a roughly constant screen size at any zoom.
/// Pure — the composer turns the handles into <see cref="ViewportLine"/>s through the existing line batch
/// (one render path, ADR-0028 / ADR-0033), and the controller projects them for hit-testing.
/// </summary>
public static class TranslateGizmo
{
    /// <summary>Handle length as a fraction of the camera's orbit distance, so the gizmo stays a roughly
    /// constant on-screen size as the user dollies in and out.</summary>
    public const float HandleLengthFactor = 0.18f;

    public static float HandleLength(float cameraDistance) => cameraDistance * HandleLengthFactor;

    /// <summary>The three axis handles at <paramref name="origin"/>, each running one
    /// <paramref name="length"/> along its positive world axis.</summary>
    public static IReadOnlyList<GizmoHandle> Handles(Vector3 origin, float length) => new[]
    {
        new GizmoHandle(GizmoAxis.X, origin, origin + new Vector3(length, 0f, 0f)),
        new GizmoHandle(GizmoAxis.Y, origin, origin + new Vector3(0f, length, 0f)),
        new GizmoHandle(GizmoAxis.Z, origin, origin + new Vector3(0f, 0f, length)),
    };

    /// <summary>The unit direction of an axis (zero for <see cref="GizmoAxis.None"/>).</summary>
    public static Vector3 AxisUnit(GizmoAxis axis) => axis switch
    {
        GizmoAxis.X => Vector3.UnitX,
        GizmoAxis.Y => Vector3.UnitY,
        GizmoAxis.Z => Vector3.UnitZ,
        _ => Vector3.Zero,
    };

    /// <summary>Returns <paramref name="position"/> shifted by <paramref name="delta"/> along
    /// <paramref name="axis"/>, in the document's <see cref="Float3"/> space.</summary>
    public static Float3 Translate(Float3 position, GizmoAxis axis, float delta) => axis switch
    {
        GizmoAxis.X => position with { X = position.X + delta },
        GizmoAxis.Y => position with { Y = position.Y + delta },
        GizmoAxis.Z => position with { Z = position.Z + delta },
        _ => position,
    };

    /// <summary>Appends the gizmo's handles as viewport lines, colouring the active (dragged) axis with the
    /// highlight role and the others with their per-axis role.</summary>
    public static void AppendDrawLines(
        ICollection<ViewportLine> sink, Vector3 origin, float length, GizmoAxis activeAxis)
    {
        foreach (var handle in Handles(origin, length))
        {
            sink.Add(new ViewportLine(handle.Start, handle.End, GizmoAxisRoles.For(handle.Axis, activeAxis)));
        }
    }
}
