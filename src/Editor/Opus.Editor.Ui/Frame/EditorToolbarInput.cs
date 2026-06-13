using System;
using Opus.Editor.Core;
using Opus.Engine.Input;

namespace Opus.Editor.Ui;

/// <summary>
/// Routes a left-click on the window's toolbar to its button action (add node / add light / undo / redo /
/// delete / frame). Stateless and GPU-free — it rebuilds the same buttons the composer drew (from the
/// toolbar rect, the chrome strings, and the controller's toolbar state) and performs the hit button's
/// action on the controller. The toolbar sits above the viewport, so this never conflicts with the
/// viewport gesture.
/// </summary>
public static class EditorToolbarInput
{
    /// <summary>Applies one frame of input to the toolbar; returns the action performed, or
    /// <see cref="EditorToolbarAction.None"/> when no enabled button was clicked this frame.</summary>
    public static EditorToolbarAction Apply(
        IInputSource input, ViewportController controller, EditorPanelRect toolbar, EditorChromeStrings strings)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(strings);
        if (!input.IsMouseButtonPressed(MouseButton.Left))
        {
            return EditorToolbarAction.None;
        }

        var (mouseX, mouseY) = input.MousePosition;
        if (!toolbar.Contains(mouseX, mouseY))
        {
            return EditorToolbarAction.None;
        }

        var buttons = EditorToolbarButtons.Build(toolbar, strings, controller.ToolbarState);
        var action = EditorToolbarButtons.HitTest(buttons, mouseX, mouseY);
        Perform(action, controller);
        return action;
    }

    private static void Perform(EditorToolbarAction action, ViewportController controller)
    {
        switch (action)
        {
            case EditorToolbarAction.AddNode:
                controller.AddNodeAtTarget();
                break;
            case EditorToolbarAction.AddLight:
                controller.AddPointLightAtTarget();
                break;
            case EditorToolbarAction.AddCube:
                controller.AddPrimitiveAtTarget(ScenePrimitiveKind.Cube);
                break;
            case EditorToolbarAction.AddSphere:
                controller.AddPrimitiveAtTarget(ScenePrimitiveKind.Sphere);
                break;
            case EditorToolbarAction.AddCylinder:
                controller.AddPrimitiveAtTarget(ScenePrimitiveKind.Cylinder);
                break;
            case EditorToolbarAction.AddPlane:
                controller.AddPrimitiveAtTarget(ScenePrimitiveKind.Plane);
                break;
            case EditorToolbarAction.AddCone:
                controller.AddPrimitiveAtTarget(ScenePrimitiveKind.Cone);
                break;
            case EditorToolbarAction.Undo:
                controller.Undo();
                break;
            case EditorToolbarAction.Redo:
                controller.Redo();
                break;
            case EditorToolbarAction.Delete:
                controller.DeleteSelected();
                break;
            case EditorToolbarAction.Frame:
                controller.FrameSelection();
                break;
            case EditorToolbarAction.Save:
            case EditorToolbarAction.AddModel:
                // Save (file write) and AddModel (content-root listing) need IO, which the pure UI layer
                // does not perform; the returned action tells the app-layer window runner to do it.
                // Nothing happens on the controller here.
                break;
            default:
                break;
        }
    }
}
