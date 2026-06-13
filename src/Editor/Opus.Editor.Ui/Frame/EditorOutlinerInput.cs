using System;
using Opus.Editor.Core;
using Opus.Engine.Input;

namespace Opus.Editor.Ui;

/// <summary>
/// Routes outliner input: the wheel scrolls the row list (when the cursor is over the panel) and a left-click
/// selects the clicked element — node or light. Stateless and GPU-free — it rebuilds the same rows the
/// composer drew (from the panel rect, the document scene, the selection, and the controller's scroll
/// offset) and acts on the controller. The outliner is its own chrome region, so this never conflicts with
/// the toolbar or viewport gestures (the viewport's wheel-zoom is gated to the viewport rect).
/// </summary>
public static class EditorOutlinerInput
{
    /// <summary>Applies one frame of input to the outliner; returns the element selected this frame, or
    /// <see cref="SceneElementRef.None"/> when no row was clicked.</summary>
    public static SceneElementRef Apply(IInputSource input, ViewportController controller, EditorPanelRect outliner)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(controller);
        var (mouseX, mouseY) = input.MousePosition;
        if (!outliner.Contains(mouseX, mouseY))
        {
            return SceneElementRef.None;
        }

        ApplyScroll(input, controller, outliner);
        if (!input.IsMouseButtonPressed(MouseButton.Left))
        {
            return SceneElementRef.None;
        }

        var rows = EditorOutliner.Build(outliner, controller.Scene, controller.SelectedElement, controller.OutlinerScroll);
        var element = EditorOutliner.HitTest(rows, mouseX, mouseY);
        if (element.IsValid)
        {
            // A plain click selects the row; a Ctrl+click toggles it in the multi-selection, mirroring the
            // viewport's additive pick; a Shift+click selects the contiguous listed range from the current
            // primary to the clicked row (Shift wins when both modifiers are down — the range is the
            // stronger intent).
            if (input.IsKeyDown(Key.LeftShift) || input.IsKeyDown(Key.RightShift))
            {
                controller.SelectElements(
                    EditorOutliner.ElementRange(controller.Scene, controller.SelectedElement, element));
            }
            else if (input.IsKeyDown(Key.LeftControl) || input.IsKeyDown(Key.RightControl))
            {
                controller.ToggleElement(element);
            }
            else
            {
                controller.SelectElement(element);
            }
        }

        return element;
    }

    private static void ApplyScroll(IInputSource input, ViewportController controller, EditorPanelRect outliner)
    {
        int step = (int)MathF.Round(input.MouseWheelDelta);
        if (step == 0)
        {
            return;
        }

        // Wheel up (positive) scrolls toward the top (earlier rows), so it decreases the offset.
        int capacity = EditorOutliner.VisibleRowCapacity(outliner);
        int total = controller.Scene.Count + controller.Scene.LightCount;
        int maxScroll = Math.Max(0, total - capacity);
        controller.SetOutlinerScroll(Math.Clamp(controller.OutlinerScroll - step, 0, maxScroll));
    }
}
