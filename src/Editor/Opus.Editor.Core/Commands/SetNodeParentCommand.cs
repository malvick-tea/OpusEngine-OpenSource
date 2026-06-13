using System;

namespace Opus.Editor.Core;

/// <summary>
/// Re-parents a node — setting its <see cref="SceneNode.ParentId"/> to another node or to null (detach to a
/// root) — capturing the previous parent on the first apply so <see cref="Revert"/> (and a later redo)
/// round-trips exactly. The node keeps its list position and local transform; only the parent link changes.
/// The cycle / self / existence guards live in <see cref="EditorDocument.SetNodeParent"/>, so this command
/// trusts its inputs (like the other set-field commands).
/// </summary>
public sealed class SetNodeParentCommand : ISceneCommand
{
    private readonly SceneNodeId _id;
    private readonly SceneNodeId? _next;
    private SceneNodeId? _previous;
    private bool _captured;

    public SetNodeParentCommand(SceneNodeId id, SceneNodeId? next)
    {
        _id = id;
        _next = next;
    }

    public void Apply(EditorScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var node = Require(scene);
        if (!_captured)
        {
            _previous = node.ParentId;
            _captured = true;
        }

        scene.Replace(node.WithParent(_next));
    }

    public void Revert(EditorScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var node = Require(scene);
        scene.Replace(node.WithParent(_previous));
    }

    public string Describe() => $"set parent of #{_id} to {(_next is { } parent ? "#" + parent : "none")}";

    private SceneNode Require(EditorScene scene) => scene.Find(_id)
        ?? throw new InvalidOperationException($"Cannot set the parent of node #{_id}: not present.");
}
