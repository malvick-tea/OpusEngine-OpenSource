using System.Numerics;

namespace Opus.Editor.Ui;

/// <summary>The visual role of a viewport line, mapped to a colour by the render seam.</summary>
public enum ViewportLineRole
{
    /// <summary>A regular ground-grid line.</summary>
    Grid,

    /// <summary>A grid line through the origin (the X or Z axis).</summary>
    GridAxis,

    /// <summary>An edge of a placed node's bounding box.</summary>
    NodeBounds,

    /// <summary>An edge of the selected node's bounding box (highlighted).</summary>
    Selection,

    /// <summary>A line of a scene light's glyph (the star marker and its aim ray).</summary>
    Light,

    /// <summary>The translate gizmo's X-axis handle.</summary>
    GizmoX,

    /// <summary>The translate gizmo's Y-axis handle.</summary>
    GizmoY,

    /// <summary>The translate gizmo's Z-axis handle.</summary>
    GizmoZ,

    /// <summary>The translate gizmo's handle currently being dragged (highlighted).</summary>
    GizmoActive,

    /// <summary>An edge of the marquee (box select) rubber band — composed in screen space, never
    /// projected from the world.</summary>
    Marquee,
}

/// <summary>
/// A world-space line segment the editor viewport draws — a grid line, a node's wire-box edge, or the
/// selection highlight. The D3D12 seam projects the endpoints to screen and draws them; this stays
/// GPU-free.
/// </summary>
/// <param name="A">First world-space endpoint.</param>
/// <param name="B">Second world-space endpoint.</param>
/// <param name="Role">Visual role, mapped to a colour by the render seam.</param>
public readonly record struct ViewportLine(Vector3 A, Vector3 B, ViewportLineRole Role);
