using System;
using System.Collections.Generic;
using System.Globalization;
using Opus.Editor.Core;

namespace Opus.Editor.Ui;

/// <summary>One inspector row: the field it shows, its label and value text, its pixel rect, and how it
/// reacts to a click.</summary>
/// <param name="Field">The semantic field this row shows.</param>
/// <param name="Label">The field label drawn on the left (a stable code-like token, untranslated).</param>
/// <param name="Value">The value text drawn on the right (the edit buffer with a caret while editing).</param>
/// <param name="Rect">The row's pixel rect within the inspector panel.</param>
/// <param name="Editable">True when clicking the row starts an edit (numeric fields and the name).</param>
/// <param name="Editing">True when this row's field is the active numeric edit (drawn highlighted).</param>
public readonly record struct EditorInspectorRow(
    InspectorField Field, string Label, string Value, EditorPanelRect Rect, bool Editable, bool Editing);

/// <summary>
/// Lays out the properties panel for the selected element — name / asset / transform rows for a node,
/// name / kind / per-kind lighting rows for a light — and hit-tests a click against the rows. Pure: the
/// composer builds the rows for drawing and the inspector input mapper rebuilds the identical set to route
/// a click, both from the same (panel, scene, selection, edit state) inputs, so what is shown is exactly
/// what is clickable. Numeric values format invariant ("0.###") and parse back the same way, so a value
/// committed from the inspector round-trips byte-identically into the scene file and the pseudo-code
/// mirror.
/// </summary>
public static class EditorInspector
{
    public const int RowHeight = 18;
    public const int HeaderHeight = 26;

    /// <summary>The caret drawn after the edit buffer in the editing row.</summary>
    public const char EditCaret = '_';

    /// <summary>The invariant numeric format inspector values display in.</summary>
    public const string ValueFormat = "0.###";

    public static IReadOnlyList<EditorInspectorRow> Build(
        EditorPanelRect panel,
        EditorScene scene,
        SceneElementRef selection,
        FieldEditState? fieldEdit = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var rows = new List<EditorInspectorRow>();
        if (selection.IsNode && scene.Find(selection.AsNode) is { } node)
        {
            BuildNodeRows(rows, node, fieldEdit);
        }
        else if (selection.IsLight && scene.FindLight(selection.AsLight) is { } light)
        {
            BuildLightRows(rows, light, fieldEdit);
        }
        else
        {
            BuildSceneRows(rows, scene);
        }

        return LayOut(panel, rows);
    }

    /// <summary>The field of the editable row under the pixel, or <see cref="InspectorField.None"/> on a
    /// miss or over a display-only row.</summary>
    public static InspectorField HitTest(IReadOnlyList<EditorInspectorRow> rows, int pixelX, int pixelY)
    {
        ArgumentNullException.ThrowIfNull(rows);
        foreach (var row in rows)
        {
            if (row.Editable && row.Rect.Contains(pixelX, pixelY))
            {
                return row.Field;
            }
        }

        return InspectorField.None;
    }

    /// <summary>Formats a numeric field value exactly as the inspector displays it.</summary>
    public static string Format(float value) => value.ToString(ValueFormat, CultureInfo.InvariantCulture);

    /// <summary>The empty-selection state shows the scene document itself: its name (clicking starts the
    /// document rename, exactly like an element's name row) and its content counts (display-only).</summary>
    private static void BuildSceneRows(List<EditorInspectorRow> rows, EditorScene scene)
    {
        rows.Add(DisplayRow(InspectorField.Name, "scene", scene.Name, editable: true));
        rows.Add(DisplayRow(
            InspectorField.None, "nodes", scene.Count.ToString(CultureInfo.InvariantCulture), editable: false));
        rows.Add(DisplayRow(
            InspectorField.None, "lights", scene.LightCount.ToString(CultureInfo.InvariantCulture), editable: false));
    }

    private static void BuildNodeRows(List<EditorInspectorRow> rows, SceneNode node, FieldEditState? fieldEdit)
    {
        rows.Add(DisplayRow(InspectorField.Name, "name", node.Name, editable: true));

        // Empty and primitive nodes cycle their shape on click; a real content reference is display-only
        // so a stray click can never destroy an authored model path.
        bool shapeCycles = node.AssetRef is null || ScenePrimitive.TryParse(node.AssetRef) is not null;
        rows.Add(DisplayRow(InspectorField.Asset, "asset", node.AssetRef ?? "-", editable: shapeCycles));
        NumericRow(rows, fieldEdit, InspectorField.PositionX, "pos.x", node.Transform);
        NumericRow(rows, fieldEdit, InspectorField.PositionY, "pos.y", node.Transform);
        NumericRow(rows, fieldEdit, InspectorField.PositionZ, "pos.z", node.Transform);
        NumericRow(rows, fieldEdit, InspectorField.RotationX, "rot.x", node.Transform);
        NumericRow(rows, fieldEdit, InspectorField.RotationY, "rot.y", node.Transform);
        NumericRow(rows, fieldEdit, InspectorField.RotationZ, "rot.z", node.Transform);
        NumericRow(rows, fieldEdit, InspectorField.ScaleX, "scale.x", node.Transform);
        NumericRow(rows, fieldEdit, InspectorField.ScaleY, "scale.y", node.Transform);
        NumericRow(rows, fieldEdit, InspectorField.ScaleZ, "scale.z", node.Transform);
    }

    private static void BuildLightRows(List<EditorInspectorRow> rows, SceneLight light, FieldEditState? fieldEdit)
    {
        rows.Add(DisplayRow(InspectorField.Name, "name", light.Name, editable: true));
        rows.Add(DisplayRow(
            InspectorField.Kind, "kind", light.Kind.ToString().ToLowerInvariant(), editable: true));
        NumericRow(rows, fieldEdit, InspectorField.PositionX, "pos.x", light);
        NumericRow(rows, fieldEdit, InspectorField.PositionY, "pos.y", light);
        NumericRow(rows, fieldEdit, InspectorField.PositionZ, "pos.z", light);
        NumericRow(rows, fieldEdit, InspectorField.DirectionX, "dir.x", light);
        NumericRow(rows, fieldEdit, InspectorField.DirectionY, "dir.y", light);
        NumericRow(rows, fieldEdit, InspectorField.DirectionZ, "dir.z", light);
        NumericRow(rows, fieldEdit, InspectorField.ColorR, "color.r", light);
        NumericRow(rows, fieldEdit, InspectorField.ColorG, "color.g", light);
        NumericRow(rows, fieldEdit, InspectorField.ColorB, "color.b", light);
        NumericRow(rows, fieldEdit, InspectorField.Intensity, "intensity", light);
        NumericRow(rows, fieldEdit, InspectorField.Range, "range", light);
        NumericRow(rows, fieldEdit, InspectorField.SpotInner, "spot.in", light);
        NumericRow(rows, fieldEdit, InspectorField.SpotOuter, "spot.out", light);
    }

    private static void NumericRow(
        List<EditorInspectorRow> rows, FieldEditState? fieldEdit, InspectorField field, string label, EditorTransform transform)
    {
        if (InspectorFieldAccess.Read(transform, field) is { } value)
        {
            rows.Add(ValueRow(fieldEdit, field, label, value));
        }
    }

    private static void NumericRow(
        List<EditorInspectorRow> rows, FieldEditState? fieldEdit, InspectorField field, string label, SceneLight light)
    {
        if (InspectorFieldAccess.Read(light, field) is { } value)
        {
            rows.Add(ValueRow(fieldEdit, field, label, value));
        }
    }

    private static EditorInspectorRow ValueRow(
        FieldEditState? fieldEdit, InspectorField field, string label, float value)
    {
        if (fieldEdit is { } edit && edit.Field == field)
        {
            return new EditorInspectorRow(
                field, label, edit.Buffer + EditCaret, default, Editable: true, Editing: true);
        }

        return new EditorInspectorRow(field, label, Format(value), default, Editable: true, Editing: false);
    }

    private static EditorInspectorRow DisplayRow(
        InspectorField field, string label, string value, bool editable) =>
        new(field, label, value, default, editable, Editing: false);

    /// <summary>Assigns row rects top to bottom under the panel header, clipping rows past the panel's
    /// bottom exactly as the outliner does.</summary>
    private static IReadOnlyList<EditorInspectorRow> LayOut(EditorPanelRect panel, List<EditorInspectorRow> rows)
    {
        var laidOut = new List<EditorInspectorRow>(rows.Count);
        int top = panel.Y + HeaderHeight;
        int lastRowTop = panel.Bottom - RowHeight;
        foreach (var row in rows)
        {
            if (top > lastRowTop)
            {
                break;
            }

            laidOut.Add(row with { Rect = new EditorPanelRect(panel.X, top, panel.Width, RowHeight) });
            top += RowHeight;
        }

        return laidOut;
    }
}
