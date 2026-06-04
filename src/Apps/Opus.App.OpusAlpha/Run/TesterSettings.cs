using Opus.Engine.AlphaHarness.Scenes;
using Opus.Engine.Diagnostics.Overlay;

namespace Opus.App.OpusAlpha.Run;

/// <summary>
/// Tester-facing window settings that persist across alpha-host runs (Mode = Window). These are
/// engine-behaviour knobs a tester would otherwise retype as CLI flags every launch — the scene
/// density, the diagnostic-overlay verbosity, and the two opt-in performance toggles. No game
/// policy lives here (Opus stays genre-neutral); the fields mirror the window options
/// <see cref="OpusAlphaWindowRunner.BuildOptions"/> already understands.
/// <para>
/// Persisted as a versioned JSON document through
/// <see cref="Opus.Persistence.Settings.JsonSettingsSerializer"/>; the file IO + defaults policy
/// lives in <see cref="TesterSettingsStore"/>.
/// </para>
/// </summary>
/// <param name="SceneScale">Scene-density preset the host populates the window with.</param>
/// <param name="OverlayLevel">Diagnostic-overlay verbosity for the live window.</param>
/// <param name="EnableFrameBudget">When true the host runs the frame-budget watchdog.</param>
/// <param name="EnableAsyncLogging">When true the rolling log writes off the game/render thread.</param>
public sealed record TesterSettings(
    AlphaSceneScale SceneScale,
    DiagnosticOverlayLevel OverlayLevel,
    bool EnableFrameBudget,
    bool EnableAsyncLogging)
{
    /// <summary>On-disk schema version (see <see cref="Opus.Persistence.Settings.SettingsDocument{T}"/>). Bump when a field
    /// is added, removed, or re-typed so an older build rejects a newer file instead of mis-reading
    /// it.</summary>
    public const int SchemaVersion = 1;

    /// <summary>The defaults a tester inherits before editing the file: the small scene, the full
    /// overlay, and both performance toggles off — identical to
    /// <see cref="Cli.OpusAlphaArgs.WindowDefaults"/> so a settings file changes nothing until the
    /// tester edits it.</summary>
    public static TesterSettings Default { get; } = new(
        AlphaSceneScale.Small,
        DiagnosticOverlayLevel.Full,
        EnableFrameBudget: false,
        EnableAsyncLogging: false);
}
