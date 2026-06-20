using System;
using System.Collections.Generic;
using System.IO;
using Opus.Editor.Core;
using Opus.Foundation.IO;

namespace Opus.App.Editor.Run;

/// <summary>
/// The window's view of an opened project (<c>window --project</c>): the manifest's content roots and
/// scene references resolved to absolute paths against the project file's directory (the manifest stores
/// them relative to itself, the form <c>project-add</c> writes). A reference that cannot even be turned
/// into a path (illegal characters) is dropped — whether a resolved path exists on disk is the listing
/// and browsing code's concern, which already treats a missing directory or file as simply contributing
/// nothing. No IO here, so the resolution is unit-tested with plain strings.
/// </summary>
/// <param name="Name">The project's display name.</param>
/// <param name="ContentRoots">The project's content roots, absolute, manifest order kept.</param>
/// <param name="Scenes">The project's scene files, absolute, manifest order kept.</param>
public sealed record EditorProjectWorkspace(
    string Name,
    IReadOnlyList<string> ContentRoots,
    IReadOnlyList<string> Scenes)
{
    /// <summary>Resolves <paramref name="document"/>'s references against
    /// <paramref name="projectDirectory"/> (the directory holding the project file).</summary>
    public static EditorProjectWorkspace Resolve(EditorProjectDocument document, string projectDirectory)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        return new EditorProjectWorkspace(
            document.Name,
            ResolveAll(document.ContentRoots, projectDirectory),
            ResolveAll(document.Scenes, projectDirectory));
    }

    private static IReadOnlyList<string> ResolveAll(IReadOnlyList<string> references, string baseDirectory)
    {
        var resolved = new List<string>(references.Count);
        foreach (string reference in references)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                continue;
            }

            try
            {
                resolved.Add(PathContainment.ResolveUnderRoot(baseDirectory, reference));
            }
            catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
            {
                // A manifest entry that is not even a well-formed path cannot be offered anywhere.
            }
        }

        return resolved;
    }
}
