using System.Collections.Generic;

namespace Opus.Editor.Core;

/// <summary>
/// The serialisable manifest of an editor project: a display name and the workspace's referenced content
/// roots, scene documents, animation graphs, and material roots (all as project-relative or absolute
/// paths). Engine-neutral connective tissue — it lets the editor "open a project" and see everything that
/// belongs to it without a single game concept. The on-disk schema version is owned by the versioned
/// envelope <c>EditorProjectSerializer</c> writes around this payload.
/// </summary>
/// <param name="Name">Display name of the project.</param>
/// <param name="ContentRoots">Directories holding loose content or packages.</param>
/// <param name="Scenes">Scene-document file references.</param>
/// <param name="AnimationGraphs">Animation state-graph file references.</param>
/// <param name="MaterialRoots">Texture-root directories holding PBR material sets.</param>
public sealed record EditorProjectDocument(
    string Name,
    IReadOnlyList<string> ContentRoots,
    IReadOnlyList<string> Scenes,
    IReadOnlyList<string> AnimationGraphs,
    IReadOnlyList<string> MaterialRoots)
{
    public static EditorProjectDocument Empty(string name) => new(
        name, new List<string>(), new List<string>(), new List<string>(), new List<string>());
}
