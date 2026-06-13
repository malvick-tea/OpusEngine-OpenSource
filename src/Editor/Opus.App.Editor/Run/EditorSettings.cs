using Opus.Editor.Ui;

namespace Opus.App.Editor.Run;

/// <summary>
/// Editor window settings that persist across runs: the last window size, the last opened scene, and the
/// chosen chrome language, so reopening the editor restores the layout, document, and language the author
/// left. No game policy lives here (Opus stays genre-neutral). Persisted as a versioned JSON document
/// through <see cref="Opus.Persistence.Settings.JsonSettingsSerializer"/>; the file IO + defaults policy
/// lives in <see cref="EditorSettingsStore"/>.
/// </summary>
/// <param name="WindowWidth">Last window width in pixels.</param>
/// <param name="WindowHeight">Last window height in pixels.</param>
/// <param name="LastScenePath">Last opened scene path, or null when none was opened.</param>
/// <param name="Language">Last chrome language. English is enum value 0, so an older settings file that
/// predates this field loads as English without a schema bump.</param>
/// <param name="LastProjectPath">Last opened project manifest, or null when the window ran without one.
/// Additive like <paramref name="Language"/> — an older file loads as null, no schema bump.</param>
public sealed record EditorSettings(
    int WindowWidth,
    int WindowHeight,
    string? LastScenePath,
    EditorLanguage Language = EditorLanguage.English,
    string? LastProjectPath = null)
{
    public const int DefaultWindowWidth = 1280;
    public const int DefaultWindowHeight = 720;

    /// <summary>On-disk schema version (see <see cref="Opus.Persistence.Settings.SettingsDocument{T}"/>).
    /// Bump when a field is added, removed, or re-typed so an older build rejects a newer file instead of
    /// mis-reading it.</summary>
    public const int SchemaVersion = 1;

    /// <summary>The defaults inherited before anything is persisted: the default window size and no
    /// remembered scene.</summary>
    public static EditorSettings Default { get; } = new(DefaultWindowWidth, DefaultWindowHeight, null);
}
