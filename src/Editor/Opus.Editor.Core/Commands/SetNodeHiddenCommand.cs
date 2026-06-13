using System;

namespace Opus.Editor.Core;

/// <summary>
/// Sets a node's editor visibility (hidden or shown), capturing the previous state on the first apply so
/// <see cref="Revert"/> (and a later redo) round-trips exactly.
/// </summary>
public sealed class SetNodeHiddenCommand : ISceneCommand
{
    private readonly SceneNodeId _id;
    private readonly bool _next;
    private bool _previous;
    private bool _captured;

    public SetNodeHiddenCommand(SceneNodeId id, bool next)
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
            _previous = node.Hidden;
            _captured = true;
        }

        scene.Replace(node.WithHidden(_next));
    }

    public void Revert(EditorScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var node = Require(scene);
        scene.Replace(node.WithHidden(_previous));
    }

    public string Describe() => $"set hidden of #{_id} to {_next}";

    private SceneNode Require(EditorScene scene) => scene.Find(_id)
        ?? throw new InvalidOperationException($"Cannot set the visibility of node #{_id}: not present.");
}
