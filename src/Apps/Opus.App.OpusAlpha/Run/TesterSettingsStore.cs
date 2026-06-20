using System;
using System.IO;
using Opus.Foundation;
using Opus.Foundation.IO;
using Opus.Persistence.Settings;

namespace Opus.App.OpusAlpha.Run;

/// <summary>
/// Loads and persists <see cref="TesterSettings"/> as a human-editable JSON file, using the engine
/// <see cref="JsonSettingsSerializer"/> for the versioned envelope and doing the file IO here (the
/// Persistence module stays IO-free by contract). The diagnostics-root pattern is followed
/// elsewhere; this store takes an explicit path so a tester can keep a profile anywhere.
/// <para>
/// Resilient at the file boundary: a missing file is seeded with defaults so the tester gets an
/// editable starting point; a corrupt or version-mismatched file is left untouched (for inspection)
/// and the run falls back to <see cref="TesterSettings.Default"/>. Settings never abort a launch.
/// </para>
/// </summary>
public static class TesterSettingsStore
{
    /// <summary>Returns the settings at <paramref name="path"/>: the parsed file when it exists and
    /// is valid; otherwise <see cref="TesterSettings.Default"/>. A missing file is seeded with the
    /// defaults (best-effort) so the tester has an editable profile next launch.</summary>
    public static TesterSettings LoadOrCreate(string path, ILog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        if (string.IsNullOrWhiteSpace(path))
        {
            return TesterSettings.Default;
        }

        if (File.Exists(path))
        {
            return Load(path, log);
        }

        if (TrySave(path, TesterSettings.Default, log))
        {
            log.Info($"Created default tester settings file: {path}");
        }

        return TesterSettings.Default;
    }

    /// <summary>Serialises <paramref name="settings"/> to <paramref name="path"/> atomically
    /// (temp file then replace). Returns false and logs when the filesystem rejects the write —
    /// a failed save never throws into the launch path.</summary>
    public static bool TrySave(string path, TesterSettings settings, ILog log)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(log);

        try
        {
            var fullPath = Path.GetFullPath(path);
            var json = JsonSettingsSerializer.Serialize(settings, TesterSettings.SchemaVersion);
            AtomicFile.WriteAllText(fullPath, json);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            log.Log(LogLevel.Warning, $"Tester settings save to '{path}' failed; settings not persisted.", ex);
            return false;
        }
    }

    private static TesterSettings Load(string path, ILog log)
    {
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.Log(LogLevel.Warning, $"Tester settings '{path}' could not be read; using defaults.", ex);
            return TesterSettings.Default;
        }

        var result = JsonSettingsSerializer.Deserialize<TesterSettings>(json, TesterSettings.SchemaVersion);
        if (result.TryGet(out var settings, out var error))
        {
            log.Info($"Loaded tester settings: {path}");
            return settings;
        }

        log.Warn(
            $"Tester settings '{path}' rejected ({error.Message}); using defaults. The file is left untouched for inspection.");
        return TesterSettings.Default;
    }
}
