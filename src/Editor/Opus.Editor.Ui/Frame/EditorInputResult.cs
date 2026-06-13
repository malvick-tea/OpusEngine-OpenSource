namespace Opus.Editor.Ui;

/// <summary>The outcome of applying one frame of viewport input.</summary>
/// <param name="QuitRequested">True when the user asked to close the editor this frame (Esc).</param>
/// <param name="ScreenshotRequested">True when the user asked to capture the frame this frame (F2).</param>
/// <param name="SaveRequested">True when the user asked to save the document this frame (Ctrl+S). The
/// actual file IO is the app layer's job — the pure input mapper only reports the request.</param>
/// <param name="NewSceneRequested">True when the user asked to start a fresh scene this frame (Ctrl+N).
/// The app layer saves the current document first and then swaps in an empty untitled one.</param>
/// <param name="OpenBrowserRequested">True when the user asked for the open-scene browser (Ctrl+O). The
/// app layer lists the scene files and opens the browser on the controller.</param>
/// <param name="OpenSceneConfirmed">True when the user confirmed the browser's highlighted scene (Enter
/// or a row click). The app layer reads the controller's choice, saves the current document, and loads
/// the chosen file.</param>
/// <param name="ModelBrowserRequested">True when the user asked for the place-model browser (M or the
/// "+ Model" toolbar button). The app layer lists the content root's model files and opens the browser on
/// the controller; confirming places the model purely on the controller, with no further app round-trip.</param>
/// <param name="SaveAsName">The file name the user committed in the save-as entry (Ctrl+Shift+S then
/// Enter), or null. The app layer writes the document to that name — the file IO half of save-as.</param>
public readonly record struct EditorInputResult(
    bool QuitRequested,
    bool ScreenshotRequested,
    bool SaveRequested,
    bool NewSceneRequested = false,
    bool OpenBrowserRequested = false,
    bool OpenSceneConfirmed = false,
    bool ModelBrowserRequested = false,
    string? SaveAsName = null);
