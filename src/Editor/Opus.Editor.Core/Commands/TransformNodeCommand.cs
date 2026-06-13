using System;

namespace Opus.Editor.Core;

/// <summary>
/// Sets a node's transform, capturing the previous transform on the first apply so <see cref="Revert"/>
/// (and a later redo) round-trips exactly.
/// </summary>
public sealed class TransformNodeCommand : ISceneCommand
{
    private readonly SceneNodeId _id;
    private readonly EditorTransform _next;
    private EditorTransform _previous;
    private bool _captured;

    public TransformNodeCommand(SceneNodeId id, EditorTransform next)
    {
        _id = id;
        _next = next;
    }

    /// <summary>Creates the command with an explicit previous transform already captured, so a coalesced
    /// drag (which previewed the scene directly across many frames) commits as a single reversible edit:
    /// undo restores <paramref name="previous"/>, redo reapplies <paramref name="next"/>.</summary>
    public TransformNodeCommand(SceneNodeId id, EditorTransform previous, EditorTransform next)
    {
        _id = id;
        _previous = previous;
        _next = next;
        _captured = true;
    }

    public void Apply(EditorScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var node = Require(scene);
        if (!_captured)
        {
            _previous = node.Transform;
            _captured = true;
        }

        scene.Replace(node.WithTransform(_next));
    }

    public void Revert(EditorScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var node = Require(scene);
        scene.Replace(node.WithTransform(_previous));
    }

    private SceneNode Require(EditorScene scene) => scene.Find(_id)
        ?? throw new InvalidOperationException($"Cannot transform node #{_id}: not present.");

    public string Describe() => $"transform #{_id}";
}
