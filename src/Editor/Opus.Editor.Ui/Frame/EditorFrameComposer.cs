using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Opus.Editor.Core;

namespace Opus.Editor.Ui;

/// <summary>
/// Builds the <see cref="EditorFrameView"/> for one frame: lays out the chrome for the window size,
/// assembles the world-space scene draw list and projects it through the camera, and gathers the toolbar
/// title, the status line, and the live pseudo-code mirror. Pure and GPU-free, so the whole "what the window
/// shows" is unit-tested without a device; the D3D12 seam only replays the result.
/// </summary>
public static class EditorFrameComposer
{
    /// <summary>Average glyph advance assumed for the 16 px toolbar title when truncating it — the chrome
    /// has no text-measurement seam, so the composer budgets conservatively per character.</summary>
    private const int ToolbarTitleCharWidthPixels = 9;

    /// <summary>Pixel gap kept between the title's end and the action button group.</summary>
    private const int ToolbarTitleEndGapPixels = 8;

    public static EditorFrameView Compose(
        EditorDocument document,
        OrbitCamera camera,
        IModelBoundsSource bounds,
        int windowWidth,
        int windowHeight,
        GizmoAxis activeGizmoAxis = GizmoAxis.None,
        EditorChromeStrings? strings = null,
        GizmoMode gizmoMode = GizmoMode.Translate,
        int outlinerScroll = 0,
        RenameState? rename = null,
        bool helpVisible = false,
        FieldEditState? fieldEdit = null,
        SceneBrowserState? sceneBrowser = null,
        int mirrorScroll = 0,
        bool statsVisible = false,
        MarqueeState? marquee = null,
        SaveAsState? saveAs = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(bounds);
        strings ??= EditorChromeStrings.English;

        var chrome = EditorChromeLayout.Build(windowWidth, windowHeight);
        var worldLines = new List<ViewportLine>(
            ViewportSceneDrawList.Build(document.Scene, bounds, document.SelectedElements));
        AppendSelectionGizmo(worldLines, document, camera, activeGizmoAxis, gizmoMode);
        var projected = new List<ProjectedLine>(
            EditorViewportProjection.Project(camera, chrome.Viewport, worldLines));
        projected.AddRange(ViewportGnomon.Build(camera, chrome.Viewport));
        if (marquee is { } box)
        {
            AppendMarquee(projected, chrome.Viewport, box);
        }

        var pseudoCode = BuildMirrorWindow(document, chrome.DslPanel, mirrorScroll);
        var toolbarState = new EditorToolbarState(
            document.CanUndo, document.CanRedo, document.SelectedElement.IsValid, document.IsDirty);
        var toolbarButtons = EditorToolbarButtons.Build(chrome.Toolbar, strings, toolbarState);
        var outlinerRows = EditorOutliner.Build(
            chrome.Outliner, document.Scene, document.SelectedElements, outlinerScroll, rename, strings);
        var inspectorRows = EditorInspector.Build(
            chrome.Inspector, document.Scene, document.SelectedElement, fieldEdit);
        var help = helpVisible ? EditorHelpOverlay.Build(chrome.Viewport, strings.Language) : null;
        var browser = sceneBrowser is null
            ? null
            : EditorSceneBrowser.Build(chrome.Viewport, sceneBrowser, strings);
        var stats = statsVisible
            ? EditorStatsOverlay.Build(chrome.Viewport, document, camera, gizmoMode, strings.Language)
            : null;
        return new EditorFrameView(
            chrome, projected, BuildToolbarText(document, strings, chrome.Toolbar), strings.PseudoCodeHeader, pseudoCode,
            BuildStatusText(document, strings, gizmoMode, rename, fieldEdit, saveAs), toolbarButtons, strings.OutlinerHeader,
            outlinerRows, strings.InspectorHeader, inspectorRows, help, browser, stats);
    }

    /// <summary>Slices the mirror text to the scrolled window. The offset is clamped so an overscroll
    /// (the wheel ran past the end, or the mirror shrank after an edit) lands on the last full page,
    /// exactly like the outliner; the drawer clips the bottom, so dropping leading lines is the whole
    /// scroll. At offset zero the full mirror passes through unchanged.</summary>
    private static string[] BuildMirrorWindow(EditorDocument document, EditorPanelRect dslPanel, int mirrorScroll)
    {
        var lines = document.ToPseudoCode().Split('\n', StringSplitOptions.None);
        int capacity = EditorFrameDrawer.MirrorLineCapacity(dslPanel);
        int start = Math.Clamp(mirrorScroll, 0, Math.Max(0, lines.Length - capacity));
        return start == 0 ? lines : lines[start..];
    }

    /// <summary>Appends the marquee rectangle — the rubber band of an in-progress Shift+drag box select —
    /// as four screen-space lines over the viewport. The marquee lives in normalised viewport coordinates,
    /// so this maps it to window pixels; it is the one line set that never passes through the camera.</summary>
    private static void AppendMarquee(List<ProjectedLine> projected, EditorPanelRect viewport, MarqueeState box)
    {
        var min = new Vector2(viewport.X + (box.Min.X * viewport.Width), viewport.Y + (box.Min.Y * viewport.Height));
        var max = new Vector2(viewport.X + (box.Max.X * viewport.Width), viewport.Y + (box.Max.Y * viewport.Height));
        var topRight = new Vector2(max.X, min.Y);
        var bottomLeft = new Vector2(min.X, max.Y);
        projected.Add(new ProjectedLine(min, topRight, ViewportLineRole.Marquee));
        projected.Add(new ProjectedLine(topRight, max, ViewportLineRole.Marquee));
        projected.Add(new ProjectedLine(max, bottomLeft, ViewportLineRole.Marquee));
        projected.Add(new ProjectedLine(bottomLeft, min, ViewportLineRole.Marquee));
    }

    private static void AppendSelectionGizmo(
        List<ViewportLine> worldLines,
        EditorDocument document,
        OrbitCamera camera,
        GizmoAxis activeGizmoAxis,
        GizmoMode gizmoMode)
    {
        if (ElementGizmo.Origin(document, gizmoMode) is not { } origin)
        {
            return;
        }

        float length = TranslateGizmo.HandleLength(camera.Distance);
        switch (gizmoMode)
        {
            case GizmoMode.Scale:
                ScaleGizmo.AppendDrawLines(worldLines, origin, length, activeGizmoAxis);
                break;
            case GizmoMode.Rotate:
                RotateGizmo.AppendDrawLines(worldLines, origin, length, activeGizmoAxis);
                break;
            default:
                TranslateGizmo.AppendDrawLines(worldLines, origin, length, activeGizmoAxis);
                break;
        }
    }

    private static string BuildToolbarText(
        EditorDocument document, EditorChromeStrings strings, EditorPanelRect toolbar)
    {
        string title = $"{strings.ApplicationName}  —  {document.Name}";
        if (document.IsDirty)
        {
            title = $"{title} {strings.DirtyMarker}";
        }

        // On a narrow window the title would run under the right-aligned action buttons; truncate it with
        // an ellipsis so the document name clips cleanly instead of disappearing behind a button.
        int available = EditorToolbarButtons.ActionGroupStartX(toolbar)
            - EditorToolbarButtons.TitleStartX(toolbar) - ToolbarTitleEndGapPixels;
        int maxChars = available / ToolbarTitleCharWidthPixels;
        if (title.Length <= maxChars)
        {
            return title;
        }

        return maxChars <= 3 ? string.Empty : title[..(maxChars - 3)] + "...";
    }

    private static string BuildStatusText(
        EditorDocument document,
        EditorChromeStrings strings,
        GizmoMode gizmoMode,
        RenameState? rename,
        FieldEditState? fieldEdit,
        SaveAsState? saveAs)
    {
        string selectionText = BuildSelectionText(document, strings, rename, fieldEdit, saveAs);
        string gizmoText = $"{strings.GizmoLabel} {strings.GizmoModeName(gizmoMode)}";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{strings.NodesLabel} {document.Scene.Count}  {strings.LightsLabel} {document.Scene.LightCount}  |  {gizmoText}  |  {selectionText}  |  {strings.ControlsHint}");
    }

    private static string BuildSelectionText(
        EditorDocument document,
        EditorChromeStrings strings,
        RenameState? rename,
        FieldEditState? fieldEdit,
        SaveAsState? saveAs)
    {
        if (rename is { } active)
        {
            return $"{strings.RenamingLabel} {active.Buffer}{EditorOutliner.RenameCaret}";
        }

        if (fieldEdit is { } edit)
        {
            return $"{strings.EditingLabel} {edit.Buffer}{EditorInspector.EditCaret}";
        }

        if (saveAs is { } naming)
        {
            return $"{strings.SaveAsLabel} {naming.Buffer}{EditorOutliner.RenameCaret}";
        }

        if (document.SelectedElements.Count > 1)
        {
            // A multi-selection has no single name to print; the count is the useful fact.
            return string.Create(
                CultureInfo.InvariantCulture, $"{strings.SelectedCountLabel} {document.SelectedElements.Count}");
        }

        var element = document.SelectedElement;
        if (element.IsNode && document.Scene.Find(element.AsNode) is { } node)
        {
            return $"{strings.SelectedPrefix}{node.Id.Value} {node.Name}";
        }

        if (element.IsLight && document.Scene.FindLight(element.AsLight) is { } light)
        {
            return $"{strings.SelectedLightPrefix}{light.Id.Value} {light.Name}";
        }

        return strings.NoSelection;
    }
}
