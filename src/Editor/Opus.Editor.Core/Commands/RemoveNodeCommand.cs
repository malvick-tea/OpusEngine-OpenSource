using System;

namespace Opus.Editor.Core;

/// <summary>
/// Removes a node by id, capturing its value and list position on apply so <see cref="Revert"/> restores
/// it at exactly the same index — keeping node order (and therefore the pseudo-code mirror) stable across
/// undo.
/// </summary>
public sealed class RemoveNodeCommand : ISceneCommand
{
    private readonly SceneNodeId _id;
    private SceneNode? _removed;
    private int _index = -1;

    public RemoveNodeCommand(SceneNodeId id)
    {
        _id = id;
    }

    public void Apply(EditorScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        _index = scene.IndexOf(_id);
        if (_index < 0)
        {
            throw new InvalidOperationException($"Cannot remove node #{_id}: not present.");
        }

        _removed = scene.RemoveAt(_index);
    }

    public void Revert(EditorScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (_removed is null)
        {
            throw new InvalidOperationException("Cannot revert a remove that never applied.");
        }

        scene.Insert(_index, _removed);
    }

    public string Describe() => $"remove #{_id}";
}
