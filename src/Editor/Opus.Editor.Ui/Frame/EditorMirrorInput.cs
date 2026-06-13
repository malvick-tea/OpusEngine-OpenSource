using System;
using Opus.Engine.Input;

namespace Opus.Editor.Ui;

/// <summary>
/// Routes pseudo-code mirror input: the wheel scrolls the mirror text when the cursor is over the panel,
/// so a scene whose mirror outgrows the panel stays fully readable. Stateless and GPU-free — it clamps the
/// controller's scroll offset against the same line capacity the drawer clips with, mirroring how the
/// outliner input mapper drives the outliner scroll.
/// </summary>
public static class EditorMirrorInput
{
    public static void Apply(IInputSource input, ViewportController controller, EditorPanelRect dslPanel)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(controller);
        var (mouseX, mouseY) = input.MousePosition;
        int step = (int)MathF.Round(input.MouseWheelDelta);
        if (step == 0 || !dslPanel.Contains(mouseX, mouseY))
        {
            return;
        }

        // Wheel up (positive) scrolls toward the top (earlier lines), so it decreases the offset.
        int capacity = EditorFrameDrawer.MirrorLineCapacity(dslPanel);
        int maxScroll = Math.Max(0, controller.MirrorLineCount - capacity);
        controller.SetMirrorScroll(Math.Clamp(controller.MirrorScroll - step, 0, maxScroll));
    }
}
