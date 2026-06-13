using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Opus.App.Editor.Run;

/// <summary>
/// Lists the scene files the in-window open browser (Ctrl+O) offers: every "*.scene.json" in the working
/// directory plus, when the current scene lives elsewhere, its directory too — sorted by file name,
/// duplicates removed. This is the IO boundary for the browser; the pure UI only receives the resulting
/// paths. A filesystem fault yields an empty list rather than an exception, so the browser opens (empty)
/// instead of destabilising the window loop.
/// </summary>
public static class EditorSceneFileList
{
    /// <summary>The search pattern matching the editor's scene files.</summary>
    public const string Pattern = "*" + EditorUntitledScenePath.Extension;

    /// <summary>The scene files to offer, given the working directory and the path of the currently open
    /// scene (null for an untitled window).</summary>
    public static IReadOnlyList<string> List(string workingDirectory, string? currentScenePath) =>
        List(workingDirectory, currentScenePath, Array.Empty<string>());

    /// <summary>The scene files to offer for a project window: the project's own scenes lead the listing
    /// (the declared content is what the author opened the project for, and a project scene may live in a
    /// folder the directory scan never visits), with ones missing on disk skipped, then the directory
    /// listing minus duplicates.</summary>
    public static IReadOnlyList<string> List(
        string workingDirectory, string? currentScenePath, IReadOnlyList<string> projectScenes)
    {
        ArgumentNullException.ThrowIfNull(workingDirectory);
        ArgumentNullException.ThrowIfNull(projectScenes);
        var listed = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string scene in projectScenes)
        {
            if (File.Exists(scene) && seen.Add(Path.GetFullPath(scene)))
            {
                listed.Add(scene);
            }
        }

        var directories = new List<string> { workingDirectory };
        string? sceneDirectory = SafeDirectoryOf(currentScenePath);
        if (sceneDirectory is not null &&
            !string.Equals(Path.GetFullPath(sceneDirectory), Path.GetFullPath(workingDirectory), StringComparison.OrdinalIgnoreCase))
        {
            directories.Add(sceneDirectory);
        }

        var files = new List<string>();
        foreach (string directory in directories)
        {
            try
            {
                files.AddRange(Directory.EnumerateFiles(directory, Pattern));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
            {
                // An unreadable directory contributes nothing; the browser still opens with the rest.
            }
        }

        foreach (string file in files.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            if (seen.Add(Path.GetFullPath(file)))
            {
                listed.Add(file);
            }
        }

        return listed;
    }

    private static string? SafeDirectoryOf(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.GetDirectoryName(Path.GetFullPath(path));
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
        {
            return null;
        }
    }
}
