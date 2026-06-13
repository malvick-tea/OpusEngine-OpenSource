using System;
using System.Collections.Generic;
using System.IO;

namespace Opus.Editor.Ui;

/// <summary>What confirming a browser row does — the overlay is shared by every file-pick flow.</summary>
public enum BrowserPurpose
{
    /// <summary>Open the chosen scene file, replacing the current document (Ctrl+O).</summary>
    OpenScene,

    /// <summary>Place a node carrying the chosen model asset at the camera target (M / "+ Model").</summary>
    PlaceModel,
}

/// <summary>
/// The file browser's modal state: the file entries on offer (gathered by the app layer — the pure UI does
/// no IO), which entry is highlighted, and what confirming does (open a scene, place a model). While
/// non-null the window is modal: arrows move the highlight, Enter or a row click confirms, Esc closes.
/// </summary>
/// <param name="Files">The file entries in display order: full paths for <see cref="BrowserPurpose.OpenScene"/>,
/// content-root-relative asset refs for <see cref="BrowserPurpose.PlaceModel"/>.</param>
/// <param name="Highlight">Index of the highlighted entry; meaningless when <paramref name="Files"/> is empty.</param>
/// <param name="Purpose">What confirming a row does.</param>
public sealed record SceneBrowserState(
    IReadOnlyList<string> Files, int Highlight, BrowserPurpose Purpose = BrowserPurpose.OpenScene);

/// <summary>One browser row: the full path it opens, the displayed file name, its rect, and highlight.</summary>
/// <param name="Path">The full scene file path this row opens.</param>
/// <param name="Label">The displayed file name.</param>
/// <param name="Rect">The row's pixel rect within the overlay panel.</param>
/// <param name="Highlighted">True when this row is the keyboard highlight (drawn selected).</param>
public readonly record struct EditorSceneBrowserRow(string Path, string Label, EditorPanelRect Rect, bool Highlighted);

/// <summary>The composed open-scene overlay for one frame: its panel rect, localised title, and rows.</summary>
/// <param name="Panel">The overlay's pixel rect, centred in the viewport.</param>
/// <param name="Title">The localised overlay title.</param>
/// <param name="Rows">The scene file rows, top to bottom (empty when the folder has no scenes).</param>
/// <param name="EmptyHint">The localised hint drawn when there are no rows.</param>
public sealed record EditorSceneBrowserView(
    EditorPanelRect Panel, string Title, IReadOnlyList<EditorSceneBrowserRow> Rows, string EmptyHint);

/// <summary>
/// Lays out the Ctrl+O open-scene overlay — a panel centred over the viewport listing the scene files the
/// app layer found — and hit-tests a click against the rows. Pure: the composer builds the view for
/// drawing and the input mapper rebuilds the identical view to route a click, both from the same
/// (viewport, state) inputs, so what is listed is exactly what is clickable.
/// </summary>
public static class EditorSceneBrowser
{
    public const int RowHeight = 20;
    public const int HeaderHeight = 30;
    public const int PaddingX = 14;
    public const int PaddingY = 10;
    public const int PreferredWidth = 460;
    public const int ViewportMargin = 10;

    /// <summary>Lays the overlay out centred in <paramref name="viewport"/>, clamped to fit inside it.
    /// A list taller than the panel scrolls instead of clipping: the visible row window follows the
    /// highlight (derived from highlight and capacity alone, so the composer and the input mapper always
    /// agree on which rows are on screen), and the highlight is clamped onto a valid entry.</summary>
    public static EditorSceneBrowserView Build(
        EditorPanelRect viewport, SceneBrowserState state, EditorChromeStrings strings)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(strings);
        int width = Math.Min(PreferredWidth, Math.Max(0, viewport.Width - (2 * ViewportMargin)));
        int preferredHeight = HeaderHeight + (Math.Max(1, state.Files.Count) * RowHeight) + PaddingY;
        int height = Math.Min(preferredHeight, Math.Max(0, viewport.Height - (2 * ViewportMargin)));
        int x = viewport.X + ((viewport.Width - width) / 2);
        int y = viewport.Y + ((viewport.Height - height) / 2);
        var panel = new EditorPanelRect(x, y, width, height);

        var rows = new List<EditorSceneBrowserRow>(state.Files.Count);
        int top = panel.Y + HeaderHeight;
        int lastRowTop = panel.Bottom - RowHeight;
        int highlight = ClampHighlight(state);
        int first = FirstVisibleIndex(state.Files.Count, highlight, VisibleRowCapacity(panel));
        for (int index = first; index < state.Files.Count && top <= lastRowTop; index++)
        {
            // A scene entry is a full path, so the row shows its file name; a model entry is already a
            // short content-root-relative ref ("models/tank.glb"), shown whole so the folder reads too.
            string label = state.Purpose == BrowserPurpose.PlaceModel
                ? state.Files[index]
                : Path.GetFileName(state.Files[index]);
            rows.Add(new EditorSceneBrowserRow(
                state.Files[index],
                label,
                new EditorPanelRect(panel.X, top, panel.Width, RowHeight),
                index == highlight));
            top += RowHeight;
        }

        string title = state.Purpose == BrowserPurpose.PlaceModel ? strings.ModelBrowserTitle : strings.SceneBrowserTitle;
        string emptyHint = state.Purpose == BrowserPurpose.PlaceModel ? strings.ModelBrowserEmpty : strings.SceneBrowserEmpty;
        return new EditorSceneBrowserView(panel, title, rows, emptyHint);
    }

    /// <summary>The row index under the pixel, or -1 on a miss.</summary>
    public static int HitTest(IReadOnlyList<EditorSceneBrowserRow> rows, int pixelX, int pixelY)
    {
        ArgumentNullException.ThrowIfNull(rows);
        for (int index = 0; index < rows.Count; index++)
        {
            if (rows[index].Rect.Contains(pixelX, pixelY))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>The state's highlight clamped onto a valid file index (-1 when there are no files).</summary>
    public static int ClampHighlight(SceneBrowserState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Files.Count == 0 ? -1 : Math.Clamp(state.Highlight, 0, state.Files.Count - 1);
    }

    /// <summary>How many rows fit inside <paramref name="panel"/> below its header — the same arithmetic
    /// as the row loop's bottom clip, so the scroll window and the drawn rows can never disagree.</summary>
    public static int VisibleRowCapacity(EditorPanelRect panel) =>
        Math.Max(0, (panel.Height - HeaderHeight) / RowHeight);

    /// <summary>The index of the first visible row: the window scrolls just far enough to keep the
    /// highlight on screen, and never past the last full page. Pure in (count, highlight, capacity), so
    /// every caller computes the same window without retained scroll state.</summary>
    public static int FirstVisibleIndex(int count, int highlight, int capacity)
    {
        if (capacity <= 0 || count <= capacity)
        {
            return 0;
        }

        int first = Math.Max(0, highlight - capacity + 1);
        return Math.Min(first, count - capacity);
    }
}
