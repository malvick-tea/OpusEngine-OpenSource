using System;
using System.IO;
using Opus.App.Editor.Cli;
using Opus.Editor.Content;

namespace Opus.App.Editor.Run;

/// <summary>
/// Resolves and inspects a scene's asset references on disk under a content root. Shared by the content
/// report and the editor window's model-bounds source so both resolve assets identically, and the file
/// reading (a host concern) lives in one place rather than being duplicated per runner.
/// </summary>
public static class EditorModelResolver
{
    /// <summary>The content root for a command: an explicit <c>--content-root</c>, else the scene file's
    /// directory, else the current directory.</summary>
    public static string ResolveContentRoot(EditorArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (!string.IsNullOrWhiteSpace(args.ContentRoot))
        {
            return args.ContentRoot;
        }

        if (!string.IsNullOrWhiteSpace(args.ScenePath))
        {
            return Path.GetDirectoryName(Path.GetFullPath(args.ScenePath)) ?? ".";
        }

        return ".";
    }

    /// <summary>Inspects the model referenced by <paramref name="assetRef"/> under <paramref name="root"/>,
    /// or returns null when it is missing or cannot be read or parsed.</summary>
    public static ModelInspection? InspectUnderRoot(string root, string assetRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetRef);
        string path = Path.Combine(root, assetRef);
        if (!File.Exists(path))
        {
            return null;
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }

        var result = ModelInspector.TryInspect(bytes, assetRef);
        return result.IsOk ? result.Unwrap() : null;
    }
}
