using System;
using Opus.Engine.Input;

namespace Opus.Editor.Ui;

/// <summary>
/// Maps one frame of pointer / key input onto the viewport. It orchestrates the per-frame flow — route to a
/// modal handler (<see cref="EditorModalInput"/>) when one is open, else apply camera zoom / pan, the left
/// pointer gesture, and the keyboard shortcuts (<see cref="EditorKeyboardShortcuts"/>) — and owns the one
/// piece of retained state: the in-progress left gesture (so a click is told apart from an orbit drag) and
/// which gizmo axis, if any, that gesture grabbed. Pure and GPU-free — the host feeds it an
/// <see cref="IInputSource"/> and a <see cref="ViewportController"/> each frame, so the whole interaction is
/// unit-tested with a fake input source. File IO (Ctrl+S / N / O / Shift+S, F2) is never done here; it is
/// reported up through <see cref="EditorInputResult"/>.
/// </summary>
public sealed class EditorViewportInput
{
    public const float OrbitDegreesPerPixel = 0.4f;
    public const float PanFractionPerPixel = 0.0015f;
    public const float ZoomPerWheelNotch = 0.9f;
    public const int ClickMovementThresholdPixels = 4;

    private bool _leftGestureInViewport;
    private int _leftGestureMovement;
    private GizmoAxis _gizmoAxis = GizmoAxis.None;
    private bool _planarDrag;
    private bool _marquee;

    public EditorInputResult Apply(IInputSource input, ViewportController controller, EditorPanelRect viewport)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(controller);

        // A rename / field edit / save-as / browser is modal: it consumes the frame and suppresses every
        // other shortcut and pointer gesture until it ends.
        if (controller.Rename is not null)
        {
            EditorModalInput.ApplyRename(input, controller);
            return new EditorInputResult(QuitRequested: false, ScreenshotRequested: false, SaveRequested: false);
        }

        if (controller.FieldEdit is not null)
        {
            EditorModalInput.ApplyFieldEdit(input, controller);
            return new EditorInputResult(QuitRequested: false, ScreenshotRequested: false, SaveRequested: false);
        }

        if (controller.SaveAs is not null)
        {
            return EditorModalInput.ApplySaveAs(input, controller);
        }

        if (controller.SceneBrowser is not null)
        {
            return EditorModalInput.ApplySceneBrowser(input, controller, viewport);
        }

        ApplyZoom(input, controller.Camera, viewport);
        ApplyPan(input, controller.Camera);
        ApplyLeftGesture(input, controller, viewport);
        EditorKeyboardShortcuts.Apply(input, controller);

        if (EditorInputModifiers.IsChord(input, Key.S) && EditorInputModifiers.IsShiftDown(input))
        {
            // Ctrl+Shift+S opens the save-as name entry; the plain-save chord below stands down so one
            // keystroke never both saves in place and starts a save-as.
            controller.BeginSaveAs();
        }

        return new EditorInputResult(
            QuitRequested: input.IsKeyPressed(Key.Escape),
            ScreenshotRequested: input.IsKeyPressed(Key.F2),
            SaveRequested: EditorInputModifiers.IsChord(input, Key.S) && !EditorInputModifiers.IsShiftDown(input),
            NewSceneRequested: EditorInputModifiers.IsChord(input, Key.N),
            OpenBrowserRequested: EditorInputModifiers.IsChord(input, Key.O),
            ModelBrowserRequested: !EditorInputModifiers.IsControlDown(input) && input.IsKeyPressed(Key.M));
    }

    private static void ApplyZoom(IInputSource input, OrbitCamera camera, EditorPanelRect viewport)
    {
        var (mx, my) = input.MousePosition;
        if (!viewport.Contains(mx, my))
        {
            // The wheel only zooms over the 3D viewport; over the outliner it scrolls the row list instead.
            return;
        }

        float wheel = input.MouseWheelDelta;
        if (wheel != 0f)
        {
            camera.Zoom(MathF.Pow(ZoomPerWheelNotch, wheel));
        }
    }

    private static void ApplyPan(IInputSource input, OrbitCamera camera)
    {
        if (!input.IsMouseButtonDown(MouseButton.Middle))
        {
            return;
        }

        var (dx, dy) = input.MouseDelta;
        float scale = PanFractionPerPixel * camera.Distance;
        camera.Pan(-dx * scale, dy * scale);
    }

    private void ApplyLeftGesture(IInputSource input, ViewportController controller, EditorPanelRect viewport)
    {
        var (mx, my) = input.MousePosition;
        if (input.IsMouseButtonPressed(MouseButton.Left))
        {
            BeginLeftGesture(
                controller, viewport, mx, my,
                EditorInputModifiers.IsControlDown(input), EditorInputModifiers.IsShiftDown(input));
        }

        if (!_leftGestureInViewport)
        {
            return;
        }

        if (input.IsMouseButtonDown(MouseButton.Left))
        {
            ContinueLeftGesture(input, controller, viewport, mx, my);
            return;
        }

        EndLeftGesture(controller, viewport, mx, my, EditorInputModifiers.IsControlDown(input));
    }

    private void BeginLeftGesture(
        ViewportController controller, EditorPanelRect viewport, int mx, int my, bool ctrlDown, bool shiftDown)
    {
        _leftGestureInViewport = viewport.Contains(mx, my);
        _leftGestureMovement = 0;
        _gizmoAxis = GizmoAxis.None;
        _planarDrag = false;
        _marquee = false;
        if (!_leftGestureInViewport)
        {
            return;
        }

        var (x01, y01) = viewport.Normalize(mx, my);
        if (shiftDown)
        {
            // Shift declares a selection gesture: the press anchors a marquee (box select) instead of
            // grabbing a gizmo handle, the selected element, or the orbit.
            controller.BeginMarquee(x01, y01);
            _marquee = true;
            return;
        }

        var axis = controller.PickGizmoAxis(x01, y01, viewport);
        if (axis != GizmoAxis.None && controller.BeginGizmoDrag(axis, x01, y01, viewport.AspectRatio))
        {
            _gizmoAxis = axis;
            return;
        }

        // A press directly on the already selected element grabs it for a planar move (slide on its
        // ground plane); anywhere else the gesture stays an orbit / selection click. With Ctrl held the
        // press is a multi-select click in the making, so the grab stands down — snapping a planar drag
        // still works by holding Ctrl after grabbing.
        if (!ctrlDown)
        {
            _planarDrag = controller.BeginPlanarDrag(x01, y01, viewport.AspectRatio);
        }
    }

    private void ContinueLeftGesture(
        IInputSource input, ViewportController controller, EditorPanelRect viewport, int mx, int my)
    {
        if (_marquee)
        {
            var (bx01, by01) = viewport.Normalize(mx, my);
            var (bdx, bdy) = input.MouseDelta;
            _leftGestureMovement += Math.Abs(bdx) + Math.Abs(bdy);
            controller.UpdateMarquee(bx01, by01);
            return;
        }

        if (_gizmoAxis != GizmoAxis.None)
        {
            var (gx01, gy01) = viewport.Normalize(mx, my);
            controller.UpdateGizmoDrag(gx01, gy01, viewport.AspectRatio, EditorInputModifiers.IsControlDown(input));
            return;
        }

        if (_planarDrag)
        {
            var (px01, py01) = viewport.Normalize(mx, my);
            controller.UpdatePlanarDrag(px01, py01, viewport.AspectRatio, EditorInputModifiers.IsControlDown(input));
            return;
        }

        var (dx, dy) = input.MouseDelta;
        _leftGestureMovement += Math.Abs(dx) + Math.Abs(dy);
        controller.Camera.Orbit(-dx * OrbitDegreesPerPixel, -dy * OrbitDegreesPerPixel);
    }

    private void EndLeftGesture(
        ViewportController controller, EditorPanelRect viewport, int mx, int my, bool ctrlDown)
    {
        if (_marquee)
        {
            EndMarqueeGesture(controller, viewport, mx, my, ctrlDown);
        }
        else if (_gizmoAxis != GizmoAxis.None)
        {
            controller.EndGizmoDrag();
        }
        else if (_planarDrag)
        {
            // The grabbed element stays selected; a no-movement grab commits nothing.
            controller.EndPlanarDrag();
        }
        else if (_leftGestureMovement <= ClickMovementThresholdPixels && viewport.Contains(mx, my))
        {
            // The left button was released without a gizmo grab or a drag: a selection click. With Ctrl
            // held it toggles the clicked element's membership in the multi-selection instead.
            var (x01, y01) = viewport.Normalize(mx, my);
            controller.PickAt(x01, y01, viewport.AspectRatio, ctrlDown);
        }

        _gizmoAxis = GizmoAxis.None;
        _planarDrag = false;
        _marquee = false;
        _leftGestureInViewport = false;
    }

    /// <summary>Resolves a finished Shift gesture: a real drag box-selects what the rectangle contains
    /// (replace; with Ctrl also held it adds to the selection), while a Shift+click that never travelled
    /// past the click threshold toggles the clicked element's membership — the same affordance as
    /// Ctrl+click, so both selection modifiers behave alike on a plain click.</summary>
    private void EndMarqueeGesture(
        ViewportController controller, EditorPanelRect viewport, int mx, int my, bool ctrlDown)
    {
        if (_leftGestureMovement <= ClickMovementThresholdPixels)
        {
            controller.CancelMarquee();
            if (viewport.Contains(mx, my))
            {
                var (x01, y01) = viewport.Normalize(mx, my);
                controller.PickAt(x01, y01, viewport.AspectRatio, additive: true);
            }

            return;
        }

        controller.EndMarquee(viewport, ctrlDown);
    }
}
