using System.Collections.Generic;

namespace Opus.Editor.Ui;

/// <summary>
/// Everything the editor draws for one frame, computed by <see cref="EditorFrameComposer"/> from the
/// document, camera, and window size: the chrome rectangles, the projected viewport lines, the toolbar
/// title, the live pseudo-code mirror split into lines, and the status text. Pure data — <see
/// cref="EditorFrameDrawer"/> replays it onto an <see cref="Opus.Engine.Ui.IDrawSurface"/>.
/// </summary>
/// <param name="Chrome">The window's 2D panel layout.</param>
/// <param name="ViewportLines">The scene draw list projected to window pixels.</param>
/// <param name="ToolbarText">The toolbar title (document name, with a dirty marker).</param>
/// <param name="PseudoCodeHeader">The header label above the pseudo-code mirror panel.</param>
/// <param name="PseudoCodeLines">The live pseudo-code mirror, one entry per line.</param>
/// <param name="StatusText">The bottom status line (counts, selection, input hints).</param>
/// <param name="ToolbarButtons">The action toolbar buttons (undo / redo / delete / frame).</param>
/// <param name="OutlinerHeader">The header label above the scene outliner panel.</param>
/// <param name="OutlinerRows">The scene outliner rows (one per listed element).</param>
/// <param name="InspectorHeader">The header label above the selection properties panel.</param>
/// <param name="InspectorRows">The selection properties rows (empty without a selection).</param>
/// <param name="Help">The F1 shortcut-reference overlay, or null while hidden.</param>
/// <param name="SceneBrowser">The Ctrl+O open-scene overlay, or null while closed.</param>
/// <param name="Stats">The F3 developer stats overlay, or null while hidden.</param>
public sealed record EditorFrameView(
    EditorChrome Chrome,
    IReadOnlyList<ProjectedLine> ViewportLines,
    string ToolbarText,
    string PseudoCodeHeader,
    IReadOnlyList<string> PseudoCodeLines,
    string StatusText,
    IReadOnlyList<EditorToolbarButton> ToolbarButtons,
    string OutlinerHeader,
    IReadOnlyList<EditorOutlinerRow> OutlinerRows,
    string InspectorHeader,
    IReadOnlyList<EditorInspectorRow> InspectorRows,
    EditorHelpView? Help = null,
    EditorSceneBrowserView? SceneBrowser = null,
    EditorStatsView? Stats = null);
