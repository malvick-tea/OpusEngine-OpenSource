using System;
using Opus.Engine.Input;

namespace Opus.Editor.Ui;

/// <summary>
/// Routes a left-click on the inspector panel to its row's edit action: a numeric row starts a field edit
/// on the controller, the name row starts a rename, a light's kind row cycles the kind (point — spot —
/// directional), and an empty / primitive node's asset row cycles the shape — each an immediate undoable
/// edit. Stateless and GPU-free — it rebuilds the same rows the
/// composer drew (from the panel rect and the document selection) and hit-tests the click, so what is
/// shown is exactly what is clickable. The window suppresses this mapper while a modal text entry is
/// already active, exactly like the toolbar and outliner.
/// </summary>
public static class EditorInspectorInput
{
    /// <summary>Applies one frame of input to the inspector; returns the field whose edit began, or
    /// <see cref="InspectorField.None"/> when no editable row was clicked this frame.</summary>
    public static InspectorField Apply(
        IInputSource input, ViewportController controller, EditorPanelRect inspector)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(controller);
        if (!input.IsMouseButtonPressed(MouseButton.Left))
        {
            return InspectorField.None;
        }

        var (mouseX, mouseY) = input.MousePosition;
        if (!inspector.Contains(mouseX, mouseY))
        {
            return InspectorField.None;
        }

        var rows = EditorInspector.Build(inspector, controller.Scene, controller.SelectedElement);
        var field = EditorInspector.HitTest(rows, mouseX, mouseY);
        bool performed = field switch
        {
            InspectorField.None => false,
            InspectorField.Kind => controller.CycleSelectedLightKind(),
            InspectorField.Asset => controller.CycleSelectedNodeShape(),
            _ => controller.BeginFieldEdit(field),
        };
        return performed ? field : InspectorField.None;
    }
}
