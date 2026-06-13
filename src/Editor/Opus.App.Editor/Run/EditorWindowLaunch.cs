using System;
using Opus.App.Editor.Cli;
using Opus.Editor.Ui;

namespace Opus.App.Editor.Run;

/// <summary>
/// The resolved launch parameters for the editor window — which scene to open, the initial window size, and
/// the chrome language — after applying the persisted <see cref="EditorSettings"/> under an
/// explicit-argument-wins precedence: a scene or language named on the command line beats the remembered
/// one, and the window opens at the persisted size when a settings file exists, else the built-in default.
/// Pure so the precedence is unit-testable without opening a window.
/// </summary>
/// <param name="ScenePath">Scene document to open, or null to start an empty untitled document.</param>
/// <param name="Width">Initial window width in pixels.</param>
/// <param name="Height">Initial window height in pixels.</param>
/// <param name="Language">Resolved chrome language: the explicit <c>--lang</c> when given, else the
/// persisted one, else English.</param>
/// <param name="ProjectPath">Resolved project manifest: the explicit <c>--project</c> when given, else
/// the persisted one, else null (no project context).</param>
public readonly record struct EditorWindowLaunch(
    string? ScenePath, int Width, int Height, EditorLanguage Language, string? ProjectPath = null)
{
    /// <summary>Resolves the launch from the parsed <paramref name="args"/> and the optional persisted
    /// <paramref name="settings"/> (null when <c>--settings</c> was not given). An explicit
    /// <see cref="EditorArgs.ScenePath"/> / <see cref="EditorArgs.Language"/> /
    /// <see cref="EditorArgs.ProjectPath"/> wins over the remembered value; the size falls back to
    /// <see cref="EditorSettings.Default"/> so the window default has a single source of truth.</summary>
    public static EditorWindowLaunch Resolve(EditorArgs args, EditorSettings? settings)
    {
        ArgumentNullException.ThrowIfNull(args);
        var effective = settings ?? EditorSettings.Default;
        return new EditorWindowLaunch(
            args.ScenePath ?? effective.LastScenePath,
            effective.WindowWidth,
            effective.WindowHeight,
            args.Language ?? effective.Language,
            args.ProjectPath ?? effective.LastProjectPath);
    }
}
