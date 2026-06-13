using System.Numerics;
using Opus.Editor.Core;
using Opus.Engine.Input;

namespace Opus.Editor.Ui;

/// <summary>
/// Maps the editor's non-modal keyboard shortcuts onto <see cref="ViewportController"/> actions: creation
/// (1-5 / A / L), gizmo mode (W / E / R), frame / home / hide / parent (F / H / V / P), delete, the F1 / F3
/// overlays, the arrow-key nudges, and the Ctrl chords (Ctrl+A / R / D / G, Ctrl+Shift+G, Ctrl+C / V / Z / Y).
/// Pure — every shortcut is a document or UI action with no IO, so file IO (Ctrl+S, Ctrl+N, Ctrl+O) stays in
/// <see cref="EditorViewportInput"/> where it is reported up to the app layer. Bare keys fire only without
/// Ctrl so they never double-fire alongside a chord (Ctrl+R begins a rename and must not also rotate).
/// </summary>
internal static class EditorKeyboardShortcuts
{
    public static void Apply(IInputSource input, ViewportController controller)
    {
        if (!EditorInputModifiers.IsControlDown(input))
        {
            ApplyBareKeys(input, controller);
        }

        if (EditorInputModifiers.IsChord(input, Key.A))
        {
            controller.SelectAllVisible();
        }

        if (EditorInputModifiers.IsChord(input, Key.R))
        {
            controller.BeginRename();
        }

        if (EditorInputModifiers.IsChord(input, Key.D))
        {
            controller.DuplicateSelected();
        }

        if (EditorInputModifiers.IsChord(input, Key.G))
        {
            // Ctrl+Shift+G ungroups the selection; plain Ctrl+G groups it — one keystroke never does both
            // (the same shape as Ctrl+S vs Ctrl+Shift+S).
            if (EditorInputModifiers.IsShiftDown(input))
            {
                controller.UngroupSelection();
            }
            else
            {
                controller.GroupSelection();
            }
        }

        if (EditorInputModifiers.IsChord(input, Key.C))
        {
            controller.CopySelected();
        }

        if (EditorInputModifiers.IsChord(input, Key.V))
        {
            controller.PasteAtTarget();
        }

        if (EditorInputModifiers.IsChord(input, Key.Z))
        {
            controller.Undo();
        }

        if (EditorInputModifiers.IsChord(input, Key.Y))
        {
            controller.Redo();
        }
    }

    private static void ApplyBareKeys(IInputSource input, ViewportController controller)
    {
        if (input.IsKeyPressed(Key.A))
        {
            controller.AddNodeAtTarget();
        }

        if (input.IsKeyPressed(Key.L))
        {
            controller.AddPointLightAtTarget();
        }

        ApplyPrimitiveKeys(input, controller);

        if (input.IsKeyPressed(Key.W))
        {
            controller.SetGizmoMode(GizmoMode.Translate);
        }

        if (input.IsKeyPressed(Key.E))
        {
            controller.SetGizmoMode(GizmoMode.Scale);
        }

        if (input.IsKeyPressed(Key.R))
        {
            controller.SetGizmoMode(GizmoMode.Rotate);
        }

        if (input.IsKeyPressed(Key.F))
        {
            controller.FrameSelection();
        }

        if (input.IsKeyPressed(Key.H))
        {
            controller.Camera.Reset();
        }

        if (input.IsKeyPressed(Key.V))
        {
            controller.ToggleSelectedHidden();
        }

        if (input.IsKeyPressed(Key.P))
        {
            // P parents the rest of the selection under the primary; Shift+P detaches the selection to roots.
            if (EditorInputModifiers.IsShiftDown(input))
            {
                controller.UnparentSelection();
            }
            else
            {
                controller.ParentSelectionToPrimary();
            }
        }

        if (input.IsKeyPressed(Key.Delete))
        {
            controller.DeleteSelected();
        }

        if (input.IsKeyPressed(Key.F1))
        {
            controller.ToggleHelp();
        }

        if (input.IsKeyPressed(Key.F3))
        {
            controller.ToggleStats();
        }

        ApplyNudgeKeys(input, controller);
    }

    /// <summary>The arrow keys nudge the selection one grid step along the world axes — Left / Right along
    /// X, Up / Down along Z — matching the ground grid the gizmo snap rounds to. World axes rather than
    /// camera-relative, so a nudge is deterministic regardless of where the orbit ended up.</summary>
    private static void ApplyNudgeKeys(IInputSource input, ViewportController controller)
    {
        float step = GizmoSnap.TranslateStepMeters;
        if (input.IsKeyPressed(Key.Right))
        {
            controller.NudgeSelection(new Vector3(step, 0f, 0f));
        }

        if (input.IsKeyPressed(Key.Left))
        {
            controller.NudgeSelection(new Vector3(-step, 0f, 0f));
        }

        if (input.IsKeyPressed(Key.Up))
        {
            controller.NudgeSelection(new Vector3(0f, 0f, -step));
        }

        if (input.IsKeyPressed(Key.Down))
        {
            controller.NudgeSelection(new Vector3(0f, 0f, step));
        }
    }

    /// <summary>The 1–5 primitive-creation keys, in <see cref="ScenePrimitive.Kinds"/> order: cube,
    /// sphere, cylinder, plane, cone.</summary>
    private static void ApplyPrimitiveKeys(IInputSource input, ViewportController controller)
    {
        if (input.IsKeyPressed(Key.D1))
        {
            controller.AddPrimitiveAtTarget(ScenePrimitiveKind.Cube);
        }

        if (input.IsKeyPressed(Key.D2))
        {
            controller.AddPrimitiveAtTarget(ScenePrimitiveKind.Sphere);
        }

        if (input.IsKeyPressed(Key.D3))
        {
            controller.AddPrimitiveAtTarget(ScenePrimitiveKind.Cylinder);
        }

        if (input.IsKeyPressed(Key.D4))
        {
            controller.AddPrimitiveAtTarget(ScenePrimitiveKind.Plane);
        }

        if (input.IsKeyPressed(Key.D5))
        {
            controller.AddPrimitiveAtTarget(ScenePrimitiveKind.Cone);
        }
    }
}
