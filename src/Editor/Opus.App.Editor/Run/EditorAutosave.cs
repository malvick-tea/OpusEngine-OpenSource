using System;
using System.IO;
using Opus.Editor.Core;
using Opus.Foundation;

namespace Opus.App.Editor.Run;

/// <summary>
/// Keeps unsaved work when the window closes: a dirty document is written to an autosave sidecar —
/// "name.autosave.scene.json" next to the scene (or "untitled.autosave.scene.json" in the working
/// directory for an untitled window) — so quitting can never silently lose edits, while the author's real
/// scene file is never overwritten behind their back. The sidecar matches the scene-file pattern, so the
/// Ctrl+O browser offers it for recovery on the next launch. A clean document writes nothing.
/// </summary>
public static class EditorAutosave
{
    /// <summary>The marker inserted before the scene extension in an autosave sidecar's name.</summary>
    public const string Marker = ".autosave";

    /// <summary>The sidecar path for a session on <paramref name="scenePath"/> (null = untitled, which
    /// autosaves under <paramref name="directory"/>).</summary>
    public static string PathFor(string? scenePath, string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        if (string.IsNullOrWhiteSpace(scenePath))
        {
            return Path.Combine(directory, EditorUntitledScenePath.BaseName + Marker + EditorUntitledScenePath.Extension);
        }

        string name = Path.GetFileName(scenePath);
        string stem = name.EndsWith(EditorUntitledScenePath.Extension, StringComparison.OrdinalIgnoreCase)
            ? name[..^EditorUntitledScenePath.Extension.Length]
            : Path.GetFileNameWithoutExtension(name);
        string parent = Path.GetDirectoryName(scenePath) is { Length: > 0 } sceneDirectory
            ? sceneDirectory
            : directory;
        return Path.Combine(parent, stem + Marker + EditorUntitledScenePath.Extension);
    }

    /// <summary>Writes the sidecar when <paramref name="document"/> has unsaved edits; returns the written
    /// path, or null when the document was clean or the write failed (a failed autosave is logged, never
    /// thrown — shutdown must finish).</summary>
    public static string? WriteIfDirty(EditorDocument document, string? scenePath, string directory, ILog log)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(log);
        if (!document.IsDirty)
        {
            return null;
        }

        string autosavePath = PathFor(scenePath, directory);
        var saved = EditorSceneFileStore.Save(autosavePath, document.Snapshot());
        if (saved.IsErr)
        {
            log.Error(saved.UnwrapErr().Message);
            return null;
        }

        log.Info($"Unsaved changes kept: {autosavePath}");
        return autosavePath;
    }

    /// <summary>Removes the sidecar for <paramref name="scenePath"/> after a successful explicit save: the
    /// real scene file now holds the session's state, so the recovery copy would only offer outdated work
    /// in the Ctrl+O browser. A missing sidecar or a filesystem fault is tolerated (logged, never thrown).</summary>
    public static void TryDelete(string? scenePath, string directory, ILog log)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(log);
        string autosavePath = PathFor(scenePath, directory);
        try
        {
            if (File.Exists(autosavePath))
            {
                File.Delete(autosavePath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.Warn($"Stale autosave could not be removed from {autosavePath}: {ex.Message}");
        }
    }
}
