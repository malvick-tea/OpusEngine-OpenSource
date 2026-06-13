using System;
using System.Collections.Generic;

namespace Opus.Editor.Core;

/// <summary>Which kind of project reference an issue concerns.</summary>
public enum EditorProjectEntryKind
{
    ContentRoot,
    Scene,
    AnimationGraph,
    MaterialRoot,
}

/// <summary>
/// One project-validation finding: a referenced path that the existence probe could not resolve, tagged
/// with the kind of reference it was. Read-only; produced by <see cref="EditorProjectValidator"/>.
/// </summary>
/// <param name="Kind">The category of reference.</param>
/// <param name="Path">The unresolved path.</param>
public sealed record EditorProjectIssue(EditorProjectEntryKind Kind, string Path);

/// <summary>
/// Pure validation of an <see cref="EditorProjectDocument"/>: reports every referenced content root,
/// scene, animation graph, and material root that does not exist. The caller injects the existence probe
/// (which owns IO and any project-relative path resolution), so the check stays headless and the editor
/// can show all broken references at once.
/// </summary>
public static class EditorProjectValidator
{
    public static IReadOnlyList<EditorProjectIssue> Validate(
        EditorProjectDocument document, Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(exists);
        var issues = new List<EditorProjectIssue>();
        Check(document.ContentRoots, EditorProjectEntryKind.ContentRoot, exists, issues);
        Check(document.Scenes, EditorProjectEntryKind.Scene, exists, issues);
        Check(document.AnimationGraphs, EditorProjectEntryKind.AnimationGraph, exists, issues);
        Check(document.MaterialRoots, EditorProjectEntryKind.MaterialRoot, exists, issues);
        return issues;
    }

    public static bool IsValid(EditorProjectDocument document, Func<string, bool> exists) =>
        Validate(document, exists).Count == 0;

    private static void Check(
        IReadOnlyList<string> paths,
        EditorProjectEntryKind kind,
        Func<string, bool> exists,
        List<EditorProjectIssue> issues)
    {
        foreach (string path in paths)
        {
            if (!exists(path))
            {
                issues.Add(new EditorProjectIssue(kind, path));
            }
        }
    }
}
