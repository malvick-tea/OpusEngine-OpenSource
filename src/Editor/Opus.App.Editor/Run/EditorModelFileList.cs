using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Opus.App.Editor.Run;

/// <summary>
/// Lists the model files the in-window place-model browser (M / "+ Model") offers: every glTF / GLB file
/// under the content root, recursively, as content-root-relative asset refs with forward slashes — exactly
/// the form the scene document stores and the model-bounds source resolves, so a placed entry needs no
/// further path translation. This is the IO boundary for the browser; the pure UI only receives the refs.
/// A filesystem fault yields an empty list rather than an exception, so the browser opens (empty) instead
/// of destabilising the window loop.
/// </summary>
public static class EditorModelFileList
{
    /// <summary>The model file extensions the browser offers, matching the engine's glTF/GLB pipeline.</summary>
    public static readonly IReadOnlyList<string> Extensions = new[] { ".glb", ".gltf" };

    /// <summary>The model asset refs to offer, relative to <paramref name="contentRoot"/>, sorted.</summary>
    public static IReadOnlyList<string> List(string contentRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);
        return List(new[] { contentRoot });
    }

    /// <summary>The union of every root's model refs (a project window lists all its content roots),
    /// duplicates removed — a ref present under two roots is offered once and resolves from the first
    /// root holding it, the bounds source's precedence — and sorted.</summary>
    public static IReadOnlyList<string> List(IReadOnlyList<string> contentRoots)
    {
        ArgumentNullException.ThrowIfNull(contentRoots);
        var refs = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string contentRoot in contentRoots)
        {
            AppendRoot(contentRoot, refs, seen);
        }

        refs.Sort(StringComparer.OrdinalIgnoreCase);
        return refs;
    }

    private static void AppendRoot(string contentRoot, List<string> refs, HashSet<string> seen)
    {
        try
        {
            string fullRoot = Path.GetFullPath(contentRoot);
            var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
            foreach (string path in Directory.EnumerateFiles(fullRoot, "*", options))
            {
                if (Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                {
                    string assetRef = Path.GetRelativePath(fullRoot, path).Replace('\\', '/');
                    if (seen.Add(assetRef))
                    {
                        refs.Add(assetRef);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException or ArgumentException)
        {
            // An unreadable root contributes nothing; the browser still opens with the rest.
        }
    }
}
