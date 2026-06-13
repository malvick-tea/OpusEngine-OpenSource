using System;
using System.Collections.Generic;

namespace Opus.Editor.Ui;

/// <summary>An action a toolbar button performs on the document or camera.</summary>
public enum EditorToolbarAction
{
    /// <summary>No action (a click missed every button).</summary>
    None,

    /// <summary>Save the document back to its file. The actual file IO is the app layer's job (the pure UI
    /// layer is IO-free); this action is reported to the caller, which performs the write.</summary>
    Save,

    /// <summary>Undo the last edit.</summary>
    Undo,

    /// <summary>Redo the last undone edit.</summary>
    Redo,

    /// <summary>Delete the selected node.</summary>
    Delete,

    /// <summary>Frame the camera on the selection.</summary>
    Frame,

    /// <summary>Add a new empty node at the camera target.</summary>
    AddNode,

    /// <summary>Add a new point light at the camera target.</summary>
    AddLight,

    /// <summary>Add a cube primitive at the camera target.</summary>
    AddCube,

    /// <summary>Add a sphere primitive at the camera target.</summary>
    AddSphere,

    /// <summary>Add a cylinder primitive at the camera target.</summary>
    AddCylinder,

    /// <summary>Add a plane primitive at the camera target.</summary>
    AddPlane,

    /// <summary>Add a cone primitive at the camera target.</summary>
    AddCone,

    /// <summary>Open the place-model browser. Listing the content root's model files is the app layer's
    /// job (the pure UI layer is IO-free); this action is reported to the caller, which opens the browser
    /// on the controller with the files it found.</summary>
    AddModel,
}

/// <summary>The document state that decides which toolbar buttons are enabled this frame.</summary>
/// <param name="CanUndo">Whether an undo step is available.</param>
/// <param name="CanRedo">Whether a redo step is available.</param>
/// <param name="HasSelection">Whether a node is selected.</param>
/// <param name="IsDirty">Whether the document has unsaved edits (enables the Save button).</param>
public readonly record struct EditorToolbarState(bool CanUndo, bool CanRedo, bool HasSelection, bool IsDirty);

/// <summary>A laid-out toolbar button: its action, label, pixel rect, and whether it is enabled.</summary>
/// <param name="Action">The action it performs.</param>
/// <param name="Label">The localised label drawn on it.</param>
/// <param name="Rect">The button's pixel rect within the toolbar.</param>
/// <param name="Enabled">False when the action is unavailable (drawn dimmed, not clickable).</param>
public readonly record struct EditorToolbarButton(
    EditorToolbarAction Action, string Label, EditorPanelRect Rect, bool Enabled);

/// <summary>
/// Lays out the window's toolbar buttons — a left-aligned creation group (add node / add light) and a
/// right-aligned action group (save / undo / redo / delete / frame) — and hit-tests a click against them.
/// Pure — the composer builds the buttons for drawing and the toolbar input mapper builds the identical set
/// to route a click, both from the same (toolbar, strings, state) inputs, so what is drawn is exactly what
/// is clickable.
/// </summary>
public static class EditorToolbarButtons
{
    public const int ButtonWidth = 72;
    public const int ButtonGap = 6;
    public const int VerticalPadding = 4;
    public const int RightPadding = 10;
    public const int LeftPadding = 10;

    /// <summary>Pixel gap between the creation group and the toolbar title text drawn after it.</summary>
    public const int TitleGap = 16;

    /// <summary>The left-aligned creation group's actions, in draw order. This array is the single source of
    /// truth for the group's membership and count: <see cref="Build"/> maps each to its label / enabled
    /// state, and <see cref="TitleStartX"/> derives the title position from its length, so adding a button
    /// here can never drift from a hand-maintained count.</summary>
    private static readonly EditorToolbarAction[] CreationActions =
    {
        EditorToolbarAction.AddCube,
        EditorToolbarAction.AddSphere,
        EditorToolbarAction.AddCylinder,
        EditorToolbarAction.AddPlane,
        EditorToolbarAction.AddCone,
        EditorToolbarAction.AddNode,
        EditorToolbarAction.AddLight,
        EditorToolbarAction.AddModel,
    };

    /// <summary>The right-aligned action group's actions, in draw order — the single source of truth for the
    /// group's membership and count, like <see cref="CreationActions"/>; <see cref="ActionGroupStartX"/>
    /// derives the title's right limit from its length.</summary>
    private static readonly EditorToolbarAction[] ActionGroupActions =
    {
        EditorToolbarAction.Save,
        EditorToolbarAction.Undo,
        EditorToolbarAction.Redo,
        EditorToolbarAction.Delete,
        EditorToolbarAction.Frame,
    };

    public static IReadOnlyList<EditorToolbarButton> Build(
        EditorPanelRect toolbar, EditorChromeStrings strings, EditorToolbarState state)
    {
        ArgumentNullException.ThrowIfNull(strings);
        int y = toolbar.Y + VerticalPadding;
        int height = toolbar.Height - (2 * VerticalPadding);
        var buttons = new List<EditorToolbarButton>(CreationActions.Length + ActionGroupActions.Length);
        // On a toolbar too narrow for both groups, trailing creation buttons are dropped rather than
        // letting the right-aligned action group slide underneath them (overlapped buttons would draw on
        // top of each other and steal each other's clicks). The action group keeps priority — Save / Undo
        // have no keyboard-independent twin, while every creation button also exists as a key.
        int creationClipRight = ActionGroupStartX(toolbar) - ButtonGap;
        AppendRow(buttons, CreationActions, strings, state, toolbar.X + LeftPadding, y, height, creationClipRight);
        AppendRow(buttons, ActionGroupActions, strings, state, ActionGroupStartX(toolbar), y, height, int.MaxValue);
        return buttons;
    }

    /// <summary>Where the toolbar title text begins — just past the left-aligned creation group, so the
    /// title never draws under the buttons.</summary>
    public static int TitleStartX(EditorPanelRect toolbar) =>
        toolbar.X + LeftPadding + GroupWidth(CreationActions.Length) + TitleGap;

    /// <summary>Where the right-aligned action group begins — the title must end before this, so the
    /// composer truncates it to the space between the two button groups.</summary>
    public static int ActionGroupStartX(EditorPanelRect toolbar) =>
        toolbar.Right - RightPadding - GroupWidth(ActionGroupActions.Length);

    /// <summary>The action of the enabled button under the pixel, or <see cref="EditorToolbarAction.None"/>.
    /// Disabled buttons never hit.</summary>
    public static EditorToolbarAction HitTest(IReadOnlyList<EditorToolbarButton> buttons, int pixelX, int pixelY)
    {
        ArgumentNullException.ThrowIfNull(buttons);
        foreach (var button in buttons)
        {
            if (button.Enabled && button.Rect.Contains(pixelX, pixelY))
            {
                return button.Action;
            }
        }

        return EditorToolbarAction.None;
    }

    private static int GroupWidth(int buttonCount) =>
        (buttonCount * ButtonWidth) + ((buttonCount - 1) * ButtonGap);

    private static void AppendRow(
        List<EditorToolbarButton> sink,
        EditorToolbarAction[] actions,
        EditorChromeStrings strings,
        EditorToolbarState state,
        int startX,
        int y,
        int height,
        int clipRight)
    {
        for (int i = 0; i < actions.Length; i++)
        {
            int x = startX + (i * (ButtonWidth + ButtonGap));
            if (x + ButtonWidth > clipRight)
            {
                break;
            }

            sink.Add(new EditorToolbarButton(
                actions[i], Label(strings, actions[i]), new EditorPanelRect(x, y, ButtonWidth, height),
                Enabled(state, actions[i])));
        }
    }

    /// <summary>The localised label for a toolbar action.</summary>
    private static string Label(EditorChromeStrings strings, EditorToolbarAction action) => action switch
    {
        EditorToolbarAction.AddCube => strings.AddCubeButton,
        EditorToolbarAction.AddSphere => strings.AddSphereButton,
        EditorToolbarAction.AddCylinder => strings.AddCylinderButton,
        EditorToolbarAction.AddPlane => strings.AddPlaneButton,
        EditorToolbarAction.AddCone => strings.AddConeButton,
        EditorToolbarAction.AddNode => strings.AddNodeButton,
        EditorToolbarAction.AddLight => strings.AddLightButton,
        EditorToolbarAction.AddModel => strings.AddModelButton,
        EditorToolbarAction.Save => strings.SaveButton,
        EditorToolbarAction.Undo => strings.UndoButton,
        EditorToolbarAction.Redo => strings.RedoButton,
        EditorToolbarAction.Delete => strings.DeleteButton,
        EditorToolbarAction.Frame => strings.FrameButton,
        _ => string.Empty,
    };

    /// <summary>Whether a toolbar action is available this frame — creation buttons are always enabled; the
    /// action group's buttons follow the document state.</summary>
    private static bool Enabled(EditorToolbarState state, EditorToolbarAction action) => action switch
    {
        EditorToolbarAction.Save => state.IsDirty,
        EditorToolbarAction.Undo => state.CanUndo,
        EditorToolbarAction.Redo => state.CanRedo,
        EditorToolbarAction.Delete or EditorToolbarAction.Frame => state.HasSelection,
        _ => true,
    };
}
