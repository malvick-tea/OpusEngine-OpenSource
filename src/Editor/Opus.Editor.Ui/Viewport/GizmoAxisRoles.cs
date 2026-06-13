namespace Opus.Editor.Ui;

/// <summary>
/// Maps a gizmo axis to the viewport line role that colours its handle, highlighting the active (dragged)
/// axis. Shared by the translate and scale gizmos so both colour their handles identically (X / Y / Z, with
/// the active axis drawn in the highlight role).
/// </summary>
public static class GizmoAxisRoles
{
    /// <summary>The role for <paramref name="axis"/> given the currently <paramref name="activeAxis"/>
    /// (<see cref="GizmoAxis.None"/> when nothing is being dragged).</summary>
    public static ViewportLineRole For(GizmoAxis axis, GizmoAxis activeAxis)
    {
        if (axis == activeAxis)
        {
            return ViewportLineRole.GizmoActive;
        }

        return axis switch
        {
            GizmoAxis.X => ViewportLineRole.GizmoX,
            GizmoAxis.Y => ViewportLineRole.GizmoY,
            _ => ViewportLineRole.GizmoZ,
        };
    }
}
