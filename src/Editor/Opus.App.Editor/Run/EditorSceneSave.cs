using System;
using System.IO;
using Opus.Editor.Core;
using Opus.Foundation;

namespace Opus.App.Editor.Run;

/// <summary>
/// Writes the in-window document back to its scene file when the author saves (the Save toolbar button or
/// Ctrl+S). The window edits a live <see cref="EditorDocument"/> (gizmo moves, deletes, undo / redo); this is
/// the IO boundary that persists those edits, so it lives in the app layer rather than the pure UI layer.
/// Atomic write through <see cref="EditorSceneFileStore"/>; on success the document is marked saved so the
/// dirty marker clears.
/// </summary>
public static class EditorSceneSave
{
    /// <summary>Saves <paramref name="document"/> to <paramref name="scenePath"/> when there are unsaved
    /// edits. Returns true only when a write happened and succeeded. A null / empty path (an untitled
    /// window opened without a scene) or a clean document is a no-op; a filesystem fault is logged, not
    /// thrown — a failed save never destabilises the window loop.</summary>
    public static bool Save(EditorDocument document, string? scenePath, ILog log)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(log);

        if (string.IsNullOrWhiteSpace(scenePath))
        {
            log.Warn("Nothing to save to: open the window with a scene path to enable saving.");
            return false;
        }

        if (!document.IsDirty)
        {
            return false;
        }

        var saved = EditorSceneFileStore.Save(scenePath, document.Snapshot());
        if (saved.IsErr)
        {
            log.Error(saved.UnwrapErr().Message);
            return false;
        }

        document.MarkSaved();
        log.Info($"Scene saved: {scenePath}");
        return true;
    }

    /// <summary>Saves like <see cref="Save"/>, except that a DIRTY untitled document (null
    /// <paramref name="scenePath"/>) first resolves the next free untitled scene file under
    /// <paramref name="directory"/> instead of warning that there is nowhere to save — so Ctrl+S in a
    /// fresh window always works. Returns the path the window session should keep: the resolved path after
    /// a successful first save, otherwise <paramref name="scenePath"/> unchanged. A clean untitled
    /// document stays untitled (nothing to write, no path claimed).</summary>
    public static string? SaveResolvingUntitled(
        EditorDocument document, string? scenePath, string directory, Func<string, bool> exists, ILog log)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(exists);
        ArgumentNullException.ThrowIfNull(log);

        string? target = scenePath;
        if (string.IsNullOrWhiteSpace(target) && document.IsDirty)
        {
            target = EditorUntitledScenePath.Next(directory, exists);
        }

        if (target is not null && Save(document, target, log))
        {
            // The saved file supersedes any recovery sidecar from an earlier close-with-unsaved-edits;
            // keeping it would offer outdated work in the Ctrl+O browser. Only an actual write clears it —
            // a clean-document no-op may still have an unrecovered sidecar worth keeping.
            EditorAutosave.TryDelete(target, directory, log);
            return target;
        }

        return scenePath;
    }

    /// <summary>Saves the document to the file the author named in the save-as entry (Ctrl+Shift+S): the
    /// name becomes <c>&lt;name&gt;.scene.json</c> under <paramref name="directory"/> (a name already
    /// carrying the extension is kept), written even when the document is clean — a save-as is an explicit
    /// copy. Saving onto a different file that already exists is refused (the window has no overwrite
    /// confirmation, so a typo must not destroy another scene); re-saving the current file is fine.
    /// Returns the new path the session continues on, or null when nothing was written (empty / illegal
    /// name, refused overwrite, or a logged filesystem fault).</summary>
    public static string? SaveAs(
        EditorDocument document, string name, string directory, string? currentScenePath, ILog log)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(log);

        string trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            log.Warn("Save-as needs a file name; nothing was saved.");
            return null;
        }

        string fileName = trimmed.EndsWith(EditorUntitledScenePath.Extension, StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : trimmed + EditorUntitledScenePath.Extension;
        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            log.Warn($"Save-as name contains characters the filesystem rejects: {trimmed}");
            return null;
        }

        string path = Path.Combine(directory, fileName);
        bool isCurrentFile = currentScenePath is not null &&
            string.Equals(Path.GetFullPath(path), Path.GetFullPath(currentScenePath), StringComparison.OrdinalIgnoreCase);
        if (!isCurrentFile && File.Exists(path))
        {
            log.Warn($"Save-as refused: {path} already exists. Choose another name.");
            return null;
        }

        var saved = EditorSceneFileStore.Save(path, document.Snapshot());
        if (saved.IsErr)
        {
            log.Error(saved.UnwrapErr().Message);
            return null;
        }

        document.MarkSaved();
        EditorAutosave.TryDelete(path, directory, log);
        log.Info($"Scene saved: {path}");
        return path;
    }
}
