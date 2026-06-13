using Opus.Engine.Input;

namespace Opus.Editor.Ui;

/// <summary>
/// Keyboard modifier and chord detection shared by the viewport input mappers: a Ctrl chord, the Ctrl snap
/// modifier, and the Shift box-select / save-as modifier. One place so the main mapper, the shortcut
/// dispatch, and the gesture state machine all read modifiers the same way.
/// </summary>
internal static class EditorInputModifiers
{
    /// <summary>True when a Control modifier is held and <paramref name="key"/> was pressed this frame.</summary>
    public static bool IsChord(IInputSource input, Key key) => IsControlDown(input) && input.IsKeyPressed(key);

    /// <summary>True when either Control key is held — the chord modifier and the gizmo-drag snap modifier.</summary>
    public static bool IsControlDown(IInputSource input) =>
        input.IsKeyDown(Key.LeftControl) || input.IsKeyDown(Key.RightControl);

    /// <summary>True when either Shift key is held — the box-select and save-as gesture modifier.</summary>
    public static bool IsShiftDown(IInputSource input) =>
        input.IsKeyDown(Key.LeftShift) || input.IsKeyDown(Key.RightShift);
}
