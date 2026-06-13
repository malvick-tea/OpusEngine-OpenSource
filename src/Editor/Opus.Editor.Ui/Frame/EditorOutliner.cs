using System;
using System.Collections.Generic;
using System.Globalization;
using Opus.Editor.Core;

namespace Opus.Editor.Ui;

/// <summary>One outliner row: the element it represents, its label, its pixel rect, and whether it is selected.</summary>
/// <param name="Element">The element selected when the row is clicked — a node or a light.</param>
/// <param name="Label">The displayed text (id and name; lights are marked with a star).</param>
/// <param name="Rect">The row's pixel rect within the outliner panel.</param>
/// <param name="Selected">True when this row is the current selection (drawn highlighted).</param>
public readonly record struct EditorOutlinerRow(SceneElementRef Element, string Label, EditorPanelRect Rect, bool Selected);

/// <summary>
/// Lays out the scene outliner — one clickable row per scene element under the panel header (the nodes
/// first, then the lights, the selected element highlighted) — and hit-tests a click against the rows.
/// Pure: the composer builds the rows for drawing and the outliner input mapper rebuilds the identical set
/// to route a click, both from the same (panel, scene, selection) inputs, so what is listed is exactly what
/// is clickable. Rows past the panel's bottom are clipped, matching how the pseudo-code mirror clips.
/// </summary>
public static class EditorOutliner
{
    public const int RowHeight = 18;
    public const int HeaderHeight = 26;

    /// <summary>Spaces prepended to a row label per tree-depth level, so a child reads as nested under its
    /// parent. A root is at depth 0 — no indent — so a flat scene's labels are byte-identical to before.</summary>
    public const int IndentSpaces = 2;

    /// <summary>The caret drawn after the rename buffer in a renaming row.</summary>
    public const char RenameCaret = '_';

    /// <summary>Single-selection convenience over the set overload — the shape every pre-multi-select
    /// caller (and the hit-testing input mapper, whose row rects ignore selection) uses.</summary>
    public static IReadOnlyList<EditorOutlinerRow> Build(
        EditorPanelRect panel,
        EditorScene scene,
        SceneElementRef selection,
        int scrollOffset = 0,
        RenameState? rename = null,
        EditorChromeStrings? strings = null) =>
        Build(
            panel,
            scene,
            selection.IsValid ? new[] { selection } : Array.Empty<SceneElementRef>(),
            scrollOffset,
            rename,
            strings);

    public static IReadOnlyList<EditorOutlinerRow> Build(
        EditorPanelRect panel,
        EditorScene scene,
        IReadOnlyList<SceneElementRef> selection,
        int scrollOffset = 0,
        RenameState? rename = null,
        EditorChromeStrings? strings = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(selection);
        // The row rects depend only on the panel and the element count — never on the language — so the
        // input mapper may omit the strings and still hit-test exactly what the composer drew.
        strings ??= EditorChromeStrings.English;
        var listing = Listing(scene);
        int capacity = VisibleRowCapacity(panel);
        int total = listing.Count;
        int start = Math.Clamp(scrollOffset, 0, Math.Max(0, total - capacity));
        var rows = new List<EditorOutlinerRow>(Math.Min(capacity, total));
        int top = panel.Y + HeaderHeight;
        int lastRowTop = panel.Bottom - RowHeight;
        for (int index = start; index < total; index++)
        {
            if (top > lastRowTop)
            {
                break;
            }

            rows.Add(BuildRow(
                scene, selection, rename, strings, listing[index],
                new EditorPanelRect(panel.X, top, panel.Width, RowHeight)));
            top += RowHeight;
        }

        return rows;
    }

    /// <summary>How many rows fit in the panel below the header — the page size the scroll offset clamps
    /// against, so the last element can always be scrolled into view but no further.</summary>
    public static int VisibleRowCapacity(EditorPanelRect panel) =>
        Math.Max(0, (panel.Height - HeaderHeight) / RowHeight);

    /// <summary>The contiguous run of listed elements between <paramref name="from"/> and
    /// <paramref name="to"/> inclusive, in the outliner's listing order (nodes then lights) but always
    /// ending at <paramref name="to"/> — so a Shift+click range select makes the clicked row the primary.
    /// Falls back to just <paramref name="to"/> when <paramref name="from"/> is invalid or no longer
    /// listed: a range needs two ends, and a stale anchor degrades to a plain select.</summary>
    public static IReadOnlyList<SceneElementRef> ElementRange(
        EditorScene scene, SceneElementRef from, SceneElementRef to)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (!to.IsValid)
        {
            return Array.Empty<SceneElementRef>();
        }

        // Range over the same tree-ordered listing the rows are drawn in, so a Shift+click range matches
        // exactly what the user sees between the two rows.
        var entries = Listing(scene);
        var listing = new List<SceneElementRef>(entries.Count);
        foreach (var entry in entries)
        {
            listing.Add(entry.Element);
        }

        int fromIndex = listing.IndexOf(from);
        int toIndex = listing.IndexOf(to);
        if (toIndex < 0)
        {
            return Array.Empty<SceneElementRef>();
        }

        if (fromIndex < 0)
        {
            return new[] { to };
        }

        var range = listing.GetRange(Math.Min(fromIndex, toIndex), Math.Abs(toIndex - fromIndex) + 1);
        if (fromIndex > toIndex)
        {
            range.Reverse();
        }

        return range;
    }

    /// <summary>The element of the row under the pixel, or <see cref="SceneElementRef.None"/> on a miss.</summary>
    public static SceneElementRef HitTest(IReadOnlyList<EditorOutlinerRow> rows, int pixelX, int pixelY)
    {
        ArgumentNullException.ThrowIfNull(rows);
        foreach (var row in rows)
        {
            if (row.Rect.Contains(pixelX, pixelY))
            {
                return row.Element;
            }
        }

        return SceneElementRef.None;
    }

    private static EditorOutlinerRow BuildRow(
        EditorScene scene,
        IReadOnlyList<SceneElementRef> selection,
        RenameState? rename,
        EditorChromeStrings strings,
        OutlinerEntry entry,
        EditorPanelRect rect)
    {
        SceneElementRef element = entry.Element;
        string marker;
        string name;
        bool hidden;
        if (element.IsNode && scene.Find(element.AsNode) is { } node)
        {
            marker = "#";
            name = node.Name;
            hidden = node.Hidden;
        }
        else
        {
            var light = scene.FindLight(element.AsLight)!;
            marker = "*";
            name = light.Name;
            hidden = light.Hidden;
        }

        string suffix = hidden ? " " + strings.HiddenSuffix : string.Empty;
        if (rename is { } active && active.Element == element)
        {
            // The live rename buffer replaces the whole label tail; the hidden marker would only crowd it.
            name = active.Buffer + RenameCaret;
            suffix = string.Empty;
        }

        // Indent the label by tree depth so a child reads as nested under its parent (lights stay at depth 0).
        string indent = entry.Depth > 0 ? new string(' ', entry.Depth * IndentSpaces) : string.Empty;
        string label = string.Create(CultureInfo.InvariantCulture, $"{indent}{marker}{element.Id} {name}{suffix}");
        return new EditorOutlinerRow(element, label, rect, IsSelected(selection, element));
    }

    /// <summary>The outliner's element listing: nodes in tree pre-order (each root followed by its children,
    /// carrying the depth) then the lights at depth 0 — the same walk the pseudo-code mirror uses, so the
    /// two read consistently. Cycle-safe: a node left unreached by a malformed parent cycle is appended as a
    /// root, so every element is listed exactly once.</summary>
    private static List<OutlinerEntry> Listing(EditorScene scene)
    {
        var entries = new List<OutlinerEntry>(scene.Count + scene.LightCount);
        var visited = new HashSet<SceneNodeId>();
        foreach (var node in scene.Nodes)
        {
            if (SceneHierarchy.IsRoot(scene.Nodes, node))
            {
                AddNode(node, 0);
            }
        }

        foreach (var node in scene.Nodes)
        {
            if (!visited.Contains(node.Id))
            {
                AddNode(node, 0);
            }
        }

        foreach (var light in scene.Lights)
        {
            entries.Add(new OutlinerEntry(SceneElementRef.Light(light.Id), 0));
        }

        return entries;

        void AddNode(SceneNode node, int depth)
        {
            if (!visited.Add(node.Id))
            {
                return;
            }

            entries.Add(new OutlinerEntry(SceneElementRef.Node(node.Id), depth));
            foreach (var child in SceneHierarchy.ChildrenOf(scene.Nodes, node.Id))
            {
                AddNode(child, depth + 1);
            }
        }
    }

    private readonly record struct OutlinerEntry(SceneElementRef Element, int Depth);

    private static bool IsSelected(IReadOnlyList<SceneElementRef> selection, SceneElementRef element)
    {
        for (int i = 0; i < selection.Count; i++)
        {
            if (selection[i] == element)
            {
                return true;
            }
        }

        return false;
    }
}
