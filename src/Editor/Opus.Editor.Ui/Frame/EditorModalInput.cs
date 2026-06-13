using Opus.Engine.Input;

namespace Opus.Editor.Ui;

/// <summary>
/// Routes input while a modal overlay is active: a rename / inspector-field / save-as text buffer, or the
/// open-scene / place-model browser. Each is pure — typed characters and navigation drive the controller,
/// Enter commits, Esc cancels, and every other shortcut and pointer gesture stands down. Only the save-as
/// commit and an open-scene confirm flow back up as an <see cref="EditorInputResult"/> for the app layer
/// (file IO); a place-model confirm is a pure document edit completed on the controller here.
/// </summary>
internal static class EditorModalInput
{
    private static readonly EditorInputResult Consumed = new(
        QuitRequested: false, ScreenshotRequested: false, SaveRequested: false);

    /// <summary>Feeds keys to the rename buffer: Enter commits, Esc cancels (without quitting the window),
    /// Backspace deletes, and the shared key map appends typed characters.</summary>
    public static void ApplyRename(IInputSource input, ViewportController controller)
    {
        if (input.IsKeyPressed(Key.Enter))
        {
            controller.CommitRename();
            return;
        }

        if (input.IsKeyPressed(Key.Escape))
        {
            controller.CancelRename();
            return;
        }

        if (input.IsKeyPressed(Key.Backspace))
        {
            controller.RenameBackspace();
        }

        foreach (char character in RenameKeyMap.PressedCharacters(input))
        {
            controller.RenameAppend(character);
        }
    }

    /// <summary>Feeds keys to an inspector numeric field edit (modal exactly like a rename); the controller
    /// accepts only the numeric charset, so letters from the shared key map are ignored.</summary>
    public static void ApplyFieldEdit(IInputSource input, ViewportController controller)
    {
        if (input.IsKeyPressed(Key.Enter))
        {
            controller.CommitFieldEdit();
            return;
        }

        if (input.IsKeyPressed(Key.Escape))
        {
            controller.CancelFieldEdit();
            return;
        }

        if (input.IsKeyPressed(Key.Backspace))
        {
            controller.FieldEditBackspace();
        }

        foreach (char character in RenameKeyMap.PressedCharacters(input))
        {
            controller.FieldEditAppend(character);
        }
    }

    /// <summary>Feeds keys to the save-as name entry; Enter is the one key that reaches the app layer (the
    /// committed name — the file write is IO, not pure UI).</summary>
    public static EditorInputResult ApplySaveAs(IInputSource input, ViewportController controller)
    {
        if (input.IsKeyPressed(Key.Enter))
        {
            return Consumed with { SaveAsName = controller.CommitSaveAs() };
        }

        if (input.IsKeyPressed(Key.Escape))
        {
            controller.CancelSaveAs();
            return Consumed;
        }

        if (input.IsKeyPressed(Key.Backspace))
        {
            controller.SaveAsBackspace();
        }

        foreach (char character in RenameKeyMap.PressedCharacters(input))
        {
            controller.SaveAsAppend(character);
        }

        return Consumed;
    }

    /// <summary>Drives the open-scene / place-model browser: arrows and the wheel walk the highlight, Enter
    /// or a row click confirms, Esc closes. A place-model confirm places on the controller (no app
    /// round-trip); an open-scene confirm reports up so the app layer loads the file.</summary>
    public static EditorInputResult ApplySceneBrowser(
        IInputSource input, ViewportController controller, EditorPanelRect viewport)
    {
        if (input.IsKeyPressed(Key.Escape))
        {
            controller.CloseSceneBrowser();
            return Consumed;
        }

        if (input.IsKeyPressed(Key.Up))
        {
            controller.MoveSceneBrowserHighlight(-1);
        }

        if (input.IsKeyPressed(Key.Down))
        {
            controller.MoveSceneBrowserHighlight(1);
        }

        if (input.MouseWheelDelta != 0f)
        {
            // The wheel walks the highlight; the visible row window follows it (the overlay scrolls), so
            // a listing taller than the panel is fully reachable by mouse alone.
            controller.MoveSceneBrowserHighlight(input.MouseWheelDelta > 0f ? -1 : 1);
        }

        bool confirmed = input.IsKeyPressed(Key.Enter);
        if (!confirmed && input.IsMouseButtonPressed(MouseButton.Left) && controller.SceneBrowser is { } state)
        {
            // The row rects depend only on the viewport and the state — never on the language — so the
            // mapper can rebuild them with any strings instance and still match what was drawn.
            var view = EditorSceneBrowser.Build(viewport, state, EditorChromeStrings.English);
            var (mx, my) = input.MousePosition;
            int row = EditorSceneBrowser.HitTest(view.Rows, mx, my);
            if (row >= 0)
            {
                controller.SetSceneBrowserHighlight(row);
                confirmed = true;
            }
        }

        if (confirmed && controller.SceneBrowser is { Purpose: BrowserPurpose.PlaceModel })
        {
            // Placing a model is a pure document edit, so it completes on the controller here; only the
            // open-scene confirm is reported up (loading a file is the app layer's IO).
            controller.PlaceModelFromBrowser();
            return Consumed;
        }

        return Consumed with { OpenSceneConfirmed = confirmed && controller.SceneBrowserChoice is not null };
    }
}
