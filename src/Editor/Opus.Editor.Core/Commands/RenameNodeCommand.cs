using System;

namespace Opus.Editor.Core;

/// <summary>
/// Renames a node, capturing the previous name on the first apply so <see cref="Revert"/> (and a later
/// redo) round-trips exactly.
/// </summary>
public sealed class RenameNodeCommand : ISceneCommand
{
    private readonly SceneNodeId _id;
    private readonly string _next;
    private string _previous = string.Empty;
    private bool _captured;

    public RenameNodeCommand(SceneNodeId id, string next)
    {
        ArgumentException.ThrowIfNullOrEmpty(next);
        _id = id;
        _next = next;
    }

    public void Apply(EditorScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var node = Require(scene);
        if (!_captured)
        {
            _previous = node.Name;
            _captured = true;
        }

        scene.Replace(node.WithName(_next));
    }

    public void Revert(EditorScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var node = Require(scene);
        scene.Replace(node.WithName(_previous));
    }

    private SceneNode Require(EditorScene scene) => scene.Find(_id)
        ?? throw new InvalidOperationException($"Cannot rename node #{_id}: not present.");

    public string Describe() => $"rename #{_id} to \"{_next}\"";
}
