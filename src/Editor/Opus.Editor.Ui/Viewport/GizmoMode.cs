namespace Opus.Editor.Ui;

/// <summary>
/// Which transform channel the viewport gizmo edits for the selected node. The window switches mode with the
/// W (move), E (scale), and R (rotate) keys; the picking shares one axis model, so a mode change only swaps
/// how a drag is interpreted and how the handles are drawn.
/// </summary>
public enum GizmoMode
{
    /// <summary>Translate (move) the node along an axis. The default.</summary>
    Translate,

    /// <summary>Scale the node along an axis.</summary>
    Scale,

    /// <summary>Rotate the node about an axis.</summary>
    Rotate,
}
