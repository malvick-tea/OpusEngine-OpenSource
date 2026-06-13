using System;
using System.Collections.Generic;

namespace Opus.Editor.Core;

/// <summary>
/// The serialisable snapshot of an editor scene: a display name, the ordered node list, and the scene
/// lights. Engine-neutral content — no game rule, no match concept — so it lives in the editor / engine
/// tier, not a consumer repo. The on-disk schema version is owned by the versioned envelope that
/// <c>EditorSceneSerializer</c> writes around this payload, so the record itself stays a clean data shape.
/// </summary>
/// <param name="Name">Display name of the scene.</param>
/// <param name="Nodes">Ordered node list; order is preserved across save / load and undo / redo.</param>
public sealed record EditorSceneDocument(string Name, IReadOnlyList<SceneNode> Nodes)
{
    /// <summary>The scene's lights, ordered like the nodes. Modelled as an <c>init</c> property (not a
    /// constructor parameter) so a pre-lighting scene file — which carries no <c>lights</c> JSON property —
    /// deserialises to this empty default rather than null, and every existing
    /// <c>new EditorSceneDocument(name, nodes)</c> call site keeps compiling. Because the absent property
    /// falls back to the default, the additive field needs no schema-version bump (the same back-compat
    /// shape the editor settings <c>Language</c> field used).</summary>
    public IReadOnlyList<SceneLight> Lights { get; init; } = Array.Empty<SceneLight>();

    public static EditorSceneDocument Empty(string name) => new(name, Array.Empty<SceneNode>());
}
