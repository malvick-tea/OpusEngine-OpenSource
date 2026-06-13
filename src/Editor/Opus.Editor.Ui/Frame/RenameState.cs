using Opus.Editor.Core;

namespace Opus.Editor.Ui;

/// <summary>
/// The in-progress rename of one scene element: which element is being renamed and the text typed so far.
/// While a rename is active the window is modal — editing keys feed this buffer instead of the gizmo /
/// camera, Enter commits the buffer as one undoable rename, and Esc cancels. The composer draws the buffer
/// (with a caret) in the element's outliner row and the status line.
/// </summary>
/// <param name="Element">The element whose name is being edited.</param>
/// <param name="Buffer">The name text typed so far.</param>
public readonly record struct RenameState(SceneElementRef Element, string Buffer);
