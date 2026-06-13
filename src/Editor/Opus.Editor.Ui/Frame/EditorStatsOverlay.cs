using System;
using System.Collections.Generic;
using System.Globalization;
using Opus.Editor.Core;

namespace Opus.Editor.Ui;

/// <summary>One stats row: a localised label and its formatted value.</summary>
/// <param name="Label">The localised row label.</param>
/// <param name="Value">The invariant-formatted value text.</param>
public readonly record struct StatsRow(string Label, string Value);

/// <summary>The composed stats overlay for one frame: its panel rect, localised title, and rows.</summary>
/// <param name="Panel">The overlay's pixel rect, anchored in the viewport's top-right corner.</param>
/// <param name="Title">The localised overlay title.</param>
/// <param name="Rows">The stat rows, top to bottom.</param>
public sealed record EditorStatsView(EditorPanelRect Panel, string Title, IReadOnlyList<StatsRow> Rows);

/// <summary>
/// The window's F3 developer stats: a live, IO-free readout of the open document — element and hidden
/// tallies, per-kind light counts, selection size, undo / redo depth, the camera's pose, and the gizmo
/// mode — anchored in the viewport's top-right corner (the editor half of the ADR-0037 "implementation
/// information for developers" promise; the geometry-budget report stays the <c>report</c> CLI, which
/// inspects assets on disk). Pure — the composer builds the view and the drawer replays it; the table
/// owns its bilingual labels exactly like the F1 overlay.
/// </summary>
public static class EditorStatsOverlay
{
    public const int RowHeight = 18;
    public const int HeaderHeight = 28;
    public const int PaddingX = 12;
    public const int PaddingY = 8;
    public const int PanelWidth = 280;
    public const int ViewportMargin = 10;

    public static EditorStatsView Build(
        EditorPanelRect viewport,
        EditorDocument document,
        OrbitCamera camera,
        GizmoMode gizmoMode,
        EditorLanguage language)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(camera);
        var rows = BuildRows(document, camera, gizmoMode, language);
        int width = Math.Min(PanelWidth, Math.Max(0, viewport.Width - (2 * ViewportMargin)));
        int preferredHeight = HeaderHeight + (rows.Count * RowHeight) + PaddingY;
        int height = Math.Min(preferredHeight, Math.Max(0, viewport.Height - (2 * ViewportMargin)));
        int x = viewport.Right - ViewportMargin - width;
        int y = viewport.Y + ViewportMargin;
        return new EditorStatsView(new EditorPanelRect(x, y, width, height), Title(language), rows);
    }

    /// <summary>The localised overlay title.</summary>
    public static string Title(EditorLanguage language) =>
        language == EditorLanguage.Russian ? "Статистика (F3)" : "Stats (F3)";

    private static IReadOnlyList<StatsRow> BuildRows(
        EditorDocument document, OrbitCamera camera, GizmoMode gizmoMode, EditorLanguage language)
    {
        bool ru = language == EditorLanguage.Russian;
        var scene = document.Scene;
        int hiddenNodes = 0;
        int empty = 0;
        int primitives = 0;
        int models = 0;
        foreach (var node in scene.Nodes)
        {
            if (node.Hidden)
            {
                hiddenNodes++;
            }

            if (node.AssetRef is null)
            {
                empty++;
            }
            else if (ScenePrimitive.TryParse(node.AssetRef) is not null)
            {
                primitives++;
            }
            else
            {
                models++;
            }
        }

        int directional = 0;
        int point = 0;
        int spot = 0;
        int hiddenLights = 0;
        foreach (var light in scene.Lights)
        {
            if (light.Hidden)
            {
                hiddenLights++;
            }

            switch (light.Kind)
            {
                case SceneLightKind.Directional:
                    directional++;
                    break;
                case SceneLightKind.Spot:
                    spot++;
                    break;
                default:
                    point++;
                    break;
            }
        }

        var target = camera.Target;
        return new[]
        {
            new StatsRow(
                ru ? "узлы" : "nodes",
                Invariant($"{scene.Count} ({models} {(ru ? "модели" : "models")}, {primitives} {(ru ? "фигуры" : "shapes")}, {empty} {(ru ? "пустые" : "empty")})")),
            new StatsRow(
                ru ? "свет" : "lights",
                Invariant($"{scene.LightCount} ({directional} dir, {point} point, {spot} spot)")),
            new StatsRow(
                ru ? "скрыто" : "hidden",
                Invariant($"{hiddenNodes + hiddenLights}")),
            new StatsRow(
                ru ? "выбрано" : "selected",
                Invariant($"{document.SelectedElements.Count}")),
            new StatsRow(
                ru ? "отмена / повтор" : "undo / redo",
                Invariant($"{document.Commands.UndoDepth} / {document.Commands.RedoDepth}")),
            new StatsRow(
                ru ? "цель камеры" : "camera target",
                Invariant($"{target.X:0.##}, {target.Y:0.##}, {target.Z:0.##}")),
            new StatsRow(
                ru ? "камера" : "camera",
                Invariant($"{camera.Distance:0.##} m  {camera.YawDegrees:0.#}° / {camera.PitchDegrees:0.#}°")),
            new StatsRow(
                ru ? "гизмо" : "gizmo",
                gizmoMode.ToString().ToLowerInvariant()),
        };
    }

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
