using System;
using System.IO;
using Opus.Foundation;
using Opus.Foundation.IO;
using Opus.Persistence.Settings;

namespace Opus.App.Editor.Run;

/// <summary>
/// Loads and persists <see cref="EditorSettings"/> as a human-editable JSON file, using the engine
/// <see cref="JsonSettingsSerializer"/> for the versioned envelope and doing the file IO here (the
/// Persistence module stays IO-free by contract). Mirrors the alpha host's tester-settings store.
/// <para>
/// Resilient at the file boundary: a missing file is seeded with defaults so the author gets an editable
/// starting point; a corrupt or version-mismatched file is left untouched (for inspection) and the run
/// falls back to <see cref="EditorSettings.Default"/>. Settings never abort a launch.
/// </para>
/// </summary>
public static class EditorSettingsStore
{
    /// <summary>Returns the settings at <paramref name="path"/>: the parsed file when it exists and is
    /// valid; otherwise <see cref="EditorSettings.Default"/>. A missing file is seeded with the defaults
    /// (best-effort) so the author has an editable profile next launch.</summary>
    public static EditorSettings LoadOrCreate(string path, ILog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        if (string.IsNullOrWhiteSpace(path))
        {
            return EditorSettings.Default;
        }

        if (File.Exists(path))
        {
            return Load(path, log);
        }

        if (TrySave(path, EditorSettings.Default, log))
        {
            log.Info($"Created default editor settings file: {path}");
        }

        return EditorSettings.Default;
    }

    /// <summary>Serialises <paramref name="settings"/> to <paramref name="path"/> atomically (temp file
    /// then replace). Returns false and logs when the filesystem rejects the write — a failed save never
    /// throws into the launch path.</summary>
    public static bool TrySave(string path, EditorSettings settings, ILog log)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(log);

        try
        {
            var fullPath = Path.GetFullPath(path);
            var json = JsonSettingsSerializer.Serialize(settings, EditorSettings.SchemaVersion);
            AtomicFile.WriteAllText(fullPath, json);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            log.Log(LogLevel.Warning, $"Editor settings save to '{path}' failed; settings not persisted.", ex);
            return false;
        }
    }

    private static EditorSettings Load(string path, ILog log)
    {
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.Log(LogLevel.Warning, $"Editor settings '{path}' could not be read; using defaults.", ex);
            return EditorSettings.Default;
        }

        var result = JsonSettingsSerializer.Deserialize<EditorSettings>(json, EditorSettings.SchemaVersion);
        if (result.TryGet(out var settings, out var error))
        {
            log.Info($"Loaded editor settings: {path}");
            return settings;
        }

        log.Warn(
            $"Editor settings '{path}' rejected ({error.Message}); using defaults. The file is left untouched for inspection.");
        return EditorSettings.Default;
    }
}
