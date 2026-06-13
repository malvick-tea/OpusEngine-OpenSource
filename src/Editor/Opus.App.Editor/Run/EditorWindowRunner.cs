using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Opus.App.Editor.Cli;
using Opus.Editor.Core;
using Opus.Editor.Direct3D12;
using Opus.Editor.Ui;
using Opus.Engine.Input.Sdl3;
using Opus.Engine.Pal.Windows.Direct3D12;
using Opus.Foundation;

namespace Opus.App.Editor.Run;

/// <summary>
/// Runs the live D3D12 authoring window for the editor CLI's <c>window</c> command: opens a window session,
/// loads (or starts) a scene document, and drives the interactive frame loop — poll SDL input, map it onto
/// the orbit camera and selection (<see cref="EditorViewportInput"/>), compose the frame
/// (<see cref="EditorFrameComposer"/>), and render it (<see cref="EditorRenderSurface"/>) — until the
/// window closes or Esc is pressed. The rendering and input mapping are tested elsewhere (the Direct3D12
/// window smoke and the <see cref="EditorViewportInput"/> tests); this owns the scene load and the loop.
/// </summary>
public static class EditorWindowRunner
{
    public const string WindowTitle = "Opus Editor";

    /// <summary>Opens the editor window. The scene comes from <see cref="EditorArgs.ScenePath"/>, else the
    /// last scene in the settings file (when <c>--settings</c> is given), else the opened project's first
    /// scene on disk (when <c>--project</c> is given), else an empty untitled scene. With a settings file
    /// the window opens at the persisted size and saves the final size + scene on close.
    /// <paramref name="maxFrames"/> caps the loop for the live smoke; 0 runs until the window closes.</summary>
    public static int RunWindow(EditorArgs args, ILog log, int maxFrames = 0)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);

        var settings = args.SettingsPath is null ? null : EditorSettingsStore.LoadOrCreate(args.SettingsPath, log);
        var launch = EditorWindowLaunch.Resolve(args, settings);

        EditorProjectWorkspace? workspace = null;
        string? projectPath = launch.ProjectPath;
        if (!string.IsNullOrWhiteSpace(projectPath))
        {
            var loadedProject = EditorProjectFileStore.Load(projectPath);
            if (loadedProject.IsErr)
            {
                if (!string.IsNullOrWhiteSpace(args.ProjectPath))
                {
                    // The author asked for this project on the command line, so a bad file is a hard error.
                    log.Error(loadedProject.UnwrapErr().Message);
                    return EditorConsoleRunner.ExitIoFailed;
                }

                // A remembered project that has since moved or broken must not brick every launch; the
                // window opens without it, and the next settings save drops the stale reference.
                log.Warn($"Remembered project could not be opened, continuing without it: {projectPath}");
                projectPath = null;
            }
            else
            {
                string projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectPath)) is { Length: > 0 } parent
                    ? parent
                    : ".";
                workspace = EditorProjectWorkspace.Resolve(loadedProject.Unwrap(), projectDirectory);
                log.Info(
                    $"Project '{workspace.Name}': {workspace.ContentRoots.Count} content root(s), {workspace.Scenes.Count} scene(s).");
                foreach (string root in workspace.ContentRoots)
                {
                    if (!Directory.Exists(root))
                    {
                        log.Warn($"Project content root does not exist: {root}");
                    }
                }
            }
        }

        string? scenePath = launch.ScenePath;
        if (string.IsNullOrWhiteSpace(scenePath) && workspace is not null)
        {
            // The author opened a project, so its first scene on disk is the natural document to land in.
            scenePath = workspace.Scenes.FirstOrDefault(File.Exists);
        }

        var document = new EditorDocument();
        if (!string.IsNullOrWhiteSpace(scenePath))
        {
            var loaded = EditorSceneFileStore.Load(scenePath);
            if (loaded.IsErr)
            {
                log.Error(loaded.UnwrapErr().Message);
                return EditorConsoleRunner.ExitIoFailed;
            }

            document.LoadScene(loaded.Unwrap());
        }

        string recoveryPath = EditorAutosave.PathFor(scenePath, Environment.CurrentDirectory);
        if (File.Exists(recoveryPath))
        {
            log.Info($"An autosave with unsaved changes from an earlier session exists: {recoveryPath} (Ctrl+O opens it).");
        }

        // The place-model browser and bounds resolution search the explicit --content-root first (an
        // explicit argument always wins), then every project content root in manifest order.
        var contentRoots = new List<string>();
        if (!string.IsNullOrWhiteSpace(args.ContentRoot))
        {
            contentRoots.Add(args.ContentRoot);
        }

        if (workspace is not null)
        {
            contentRoots.AddRange(workspace.ContentRoots);
        }

        if (contentRoots.Count == 0)
        {
            contentRoots.Add(EditorModelResolver.ResolveContentRoot(args with { ScenePath = scenePath }));
        }

        var bounds = new EditorModelBoundsSource(contentRoots);
        var strings = EditorChromeStrings.For(launch.Language);
        var outcome = RunLoop(
            document, bounds, log, maxFrames, launch.Width, launch.Height, strings, scenePath,
            contentRoots, workspace?.Scenes ?? Array.Empty<string>());

        if (args.SettingsPath is not null && outcome.Opened)
        {
            // The remembered project is the one that actually opened (full path, so a later launch from
            // another working directory still finds it); a session without one drops any stale reference.
            string? rememberedProject = workspace is null || string.IsNullOrWhiteSpace(projectPath)
                ? null
                : Path.GetFullPath(projectPath);
            EditorSettingsStore.TrySave(
                args.SettingsPath,
                new EditorSettings(
                    outcome.FinalWidth, outcome.FinalHeight, outcome.FinalScenePath, launch.Language, rememberedProject),
                log);
        }

        return outcome.ExitCode;
    }

    private static LoopOutcome RunLoop(
        EditorDocument document,
        IModelBoundsSource bounds,
        ILog log,
        int maxFrames,
        int width,
        int height,
        EditorChromeStrings strings,
        string? scenePath,
        IReadOnlyList<string> contentRoots,
        IReadOnlyList<string> projectScenes)
    {
        var sessionOptions = D3D12WindowSessionOptions.Windowed(
            WindowTitle, width, height, enableDebugLayer: false, resizable: true);
        using var session = D3D12WindowSession.TryOpen(sessionOptions);
        if (session is null)
        {
            log.Warn("Editor window could not open: no D3D12 adapter, SDL video, swap chain, or DXC on this host.");
            return new LoopOutcome(EditorConsoleRunner.ExitIoFailed, Opened: false, width, height, scenePath);
        }

        using var surface = EditorRenderSurface.Create(session);
        // The swap chain does not track its window on its own; the bridge resizes it on the event-pump
        // thread before each frame begins. The draw surface and chrome layout read the size afresh per
        // frame, so nothing else needs rebuilding for a resize.
        using var resizeBridge = new D3D12WindowResizeBridge(session.Window, session.SwapChain.Resize);
        using var input = new SdlPolledInputSource(session.Window);
        var controller = new ViewportController(document, bounds);
        var viewportInput = new EditorViewportInput();

        bool closeRequested = false;
        void OnCloseRequested() => closeRequested = true;
        session.Window.CloseRequested += OnCloseRequested;
        log.Info(
            $"{WindowTitle} — '{document.Name}': LMB orbit, MMB pan, wheel zoom, click select, 1-5 add shape, A/L add node/light, M place model, drag a gizmo axis to move, F frame, F2 shot, Esc / close to quit.");

        try
        {
            string? currentScenePath = scenePath;
            for (int frame = 0; !closeRequested && (maxFrames == 0 || frame < maxFrames); frame++)
            {
                session.Window.PollEvents();
                var chrome = EditorChromeLayout.Build(surface.Width, surface.Height);
                // A modal text entry (rename or inspector field edit) routes keys into its buffer and the
                // click panels stand down so a stray click cannot fire toolbar actions or change the
                // selection mid-edit.
                bool modal = controller.IsModalActive;
                var toolbarAction = modal
                    ? EditorToolbarAction.None
                    : EditorToolbarInput.Apply(input, controller, chrome.Toolbar, strings);
                if (!modal)
                {
                    EditorOutlinerInput.Apply(input, controller, chrome.Outliner);
                    EditorInspectorInput.Apply(input, controller, chrome.Inspector);
                    EditorMirrorInput.Apply(input, controller, chrome.DslPanel);
                }

                var inputResult = viewportInput.Apply(input, controller, chrome.Viewport);
                if (inputResult.QuitRequested)
                {
                    break;
                }

                if (inputResult.SaveRequested || toolbarAction == EditorToolbarAction.Save)
                {
                    // An untitled window resolves its file on the first save (untitled.scene.json next to
                    // the working directory) and keeps it for the session, so Ctrl+S always works.
                    currentScenePath = EditorSceneSave.SaveResolvingUntitled(
                        document, currentScenePath, Environment.CurrentDirectory, File.Exists, log);
                }

                if (inputResult.SaveAsName is { } saveAsName)
                {
                    // A successful save-as moves the session onto the new file; a refused or failed one
                    // keeps the current path so plain Ctrl+S still goes where it always did.
                    currentScenePath = EditorSceneSave.SaveAs(
                        document, saveAsName, Environment.CurrentDirectory, currentScenePath, log)
                        ?? currentScenePath;
                }

                if (inputResult.NewSceneRequested)
                {
                    // Ctrl+N never loses work: a dirty document is saved first (an untitled one claims its
                    // file), then the window starts over with a fresh untitled scene.
                    EditorSceneSave.SaveResolvingUntitled(
                        document, currentScenePath, Environment.CurrentDirectory, File.Exists, log);
                    document.LoadScene(EditorSceneDocument.Empty(EditorScene.DefaultName));
                    currentScenePath = null;
                    log.Info("New scene.");
                }

                if (inputResult.OpenBrowserRequested)
                {
                    controller.OpenSceneBrowser(
                        EditorSceneFileList.List(Environment.CurrentDirectory, currentScenePath, projectScenes));
                }

                if (inputResult.ModelBrowserRequested || toolbarAction == EditorToolbarAction.AddModel)
                {
                    // Listing the content roots is the IO half; the placement itself happens purely on the
                    // controller when the browser confirms, so there is no second round-trip up here.
                    controller.OpenModelBrowser(EditorModelFileList.List(contentRoots));
                }

                if (inputResult.OpenSceneConfirmed && controller.SceneBrowserChoice is { } chosenScene)
                {
                    controller.CloseSceneBrowser();
                    currentScenePath = OpenScene(document, currentScenePath, chosenScene, log);
                }

                // The OS title mirrors the document (name + dirty marker) so the taskbar and Alt-Tab stay
                // truthful; only an actual change touches SDL.
                string windowTitle = EditorWindowTitle.For(document);
                if (!string.Equals(windowTitle, session.Window.Title, StringComparison.Ordinal))
                {
                    session.Window.Title = windowTitle;
                }

                var view = EditorFrameComposer.Compose(
                    document, controller.Camera, bounds, surface.Width, surface.Height,
                    controller.ActiveGizmoAxis, strings, controller.GizmoMode, controller.OutlinerScroll,
                    controller.Rename, controller.HelpVisible, controller.FieldEdit, controller.SceneBrowser,
                    controller.MirrorScroll, controller.StatsVisible, controller.Marquee, controller.SaveAs);
                RenderFrame(surface, view, inputResult.ScreenshotRequested, log);
                input.EndFrame();
            }

            // Explicit save stays the policy (the real scene file is never written behind the author's
            // back), but closing with unsaved edits keeps a recovery sidecar so quitting cannot lose work.
            EditorAutosave.WriteIfDirty(document, currentScenePath, Environment.CurrentDirectory, log);

            var (finalWidth, finalHeight) = session.Window.Size;
            return new LoopOutcome(EditorConsoleRunner.ExitOk, Opened: true, finalWidth, finalHeight, currentScenePath);
        }
        finally
        {
            session.Window.CloseRequested -= OnCloseRequested;
        }
    }

    /// <summary>Opens <paramref name="chosenScene"/> into the live document: the current document is saved
    /// first (never losing work, mirroring Ctrl+N), then the chosen file replaces it. Returns the scene
    /// path the session continues on — the chosen file on success, the previous path when the load fails
    /// (the failure is logged and the current document stays).</summary>
    private static string? OpenScene(EditorDocument document, string? currentScenePath, string chosenScene, ILog log)
    {
        EditorSceneSave.SaveResolvingUntitled(
            document, currentScenePath, Environment.CurrentDirectory, File.Exists, log);
        var loaded = EditorSceneFileStore.Load(chosenScene);
        if (loaded.IsErr)
        {
            log.Error(loaded.UnwrapErr().Message);
            return currentScenePath;
        }

        document.LoadScene(loaded.Unwrap());
        log.Info($"Scene opened: {chosenScene}");
        return chosenScene;
    }

    private static void RenderFrame(EditorRenderSurface surface, EditorFrameView view, bool capture, ILog log)
    {
        string? screenshotPath = capture ? TryPrepareScreenshotPath(log) : null;
        if (screenshotPath is null)
        {
            surface.RenderFrame(view);
            return;
        }

        try
        {
            surface.RenderFrameAndCapture(view, screenshotPath);
            log.Info($"Screenshot saved: {screenshotPath}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.Warn($"Screenshot could not be written to {screenshotPath}: {ex.Message}");
        }
    }

    private static string? TryPrepareScreenshotPath(ILog log)
    {
        try
        {
            string directory = EditorScreenshotPath.Directory();
            Directory.CreateDirectory(directory);
            return EditorScreenshotPath.Build(directory, DateTimeOffset.Now);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.Warn($"Screenshot directory could not be prepared: {ex.Message}");
            return null;
        }
    }

    /// <summary>The result of the window loop: the exit code, whether the window actually opened, the final
    /// window size, and the scene path the session ended on (an untitled window that saved gains one) — so
    /// the caller can persist the layout and reopen the same scene next launch.</summary>
    private readonly record struct LoopOutcome(
        int ExitCode, bool Opened, int FinalWidth, int FinalHeight, string? FinalScenePath);
}
