namespace Opus.Editor.Ui;

/// <summary>
/// The in-progress save-as (Ctrl+Shift+S): the file name typed so far, seeded from the document name.
/// While active the window is modal exactly like a rename — keys feed this buffer, Enter hands the name up
/// to the app layer (the file write is IO, not pure UI), and Esc cancels. The composer draws the buffer
/// (with a caret) in the status line.
/// </summary>
/// <param name="Buffer">The file name typed so far (without the scene-file extension).</param>
public readonly record struct SaveAsState(string Buffer);
