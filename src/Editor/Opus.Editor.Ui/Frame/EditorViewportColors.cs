using Opus.Engine.Ui;

namespace Opus.Editor.Ui;

/// <summary>
/// The editor window's colour palette: the chrome fills and text colours, and the per-role viewport line
/// colours. Centralised so the editor's look is one named concept rather than literals scattered through the
/// draw code, and so a later theme is a single edit.
/// </summary>
public static class EditorViewportColors
{
    public static readonly Color WindowBackground = Color.FromRgb(16, 18, 24);
    public static readonly Color ViewportBackground = Color.FromRgb(10, 12, 16);
    public static readonly Color ToolbarFill = Color.FromRgb(30, 36, 48);
    public static readonly Color PanelFill = Color.FromRgb(22, 26, 34);
    public static readonly Color StatusFill = Color.FromRgb(30, 36, 48);
    public static readonly Color PanelBorder = Color.FromRgb(64, 72, 88);
    public static readonly Color PrimaryText = Color.FromRgb(232, 236, 244);
    public static readonly Color DimText = Color.FromRgb(150, 160, 176);

    public static readonly Color Grid = Color.FromRgb(48, 54, 66);
    public static readonly Color GridAxis = Color.FromRgb(96, 120, 160);
    public static readonly Color NodeBounds = Color.FromRgb(120, 180, 230);
    public static readonly Color Selection = Color.FromRgb(255, 206, 84);
    public static readonly Color Light = Color.FromRgb(255, 224, 130);

    public static readonly Color ButtonFill = Color.FromRgb(44, 52, 68);
    public static readonly Color OutlinerSelectedFill = Color.FromRgb(46, 56, 78);

    public static readonly Color GizmoX = Color.FromRgb(228, 86, 86);
    public static readonly Color GizmoY = Color.FromRgb(120, 204, 116);
    public static readonly Color GizmoZ = Color.FromRgb(86, 132, 232);
    public static readonly Color GizmoActive = Color.FromRgb(255, 236, 150);
    public static readonly Color Marquee = Color.FromRgb(60, 230, 255);

    /// <summary>Maps a viewport line's role to its draw colour.</summary>
    public static Color ForRole(ViewportLineRole role) => role switch
    {
        ViewportLineRole.GridAxis => GridAxis,
        ViewportLineRole.NodeBounds => NodeBounds,
        ViewportLineRole.Selection => Selection,
        ViewportLineRole.Light => Light,
        ViewportLineRole.GizmoX => GizmoX,
        ViewportLineRole.GizmoY => GizmoY,
        ViewportLineRole.GizmoZ => GizmoZ,
        ViewportLineRole.GizmoActive => GizmoActive,
        ViewportLineRole.Marquee => Marquee,
        _ => Grid,
    };
}
