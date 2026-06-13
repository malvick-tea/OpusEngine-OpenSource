using System;

namespace Opus.Editor.Core;

/// <summary>
/// Adds a fully-formed node (its id already allocated from the scene) to the end of the node list.
/// <see cref="Revert"/> removes it again; redo re-adds the identical node, so its id is stable across the
/// undo / redo cycle.
/// </summary>
public sealed class PlaceNodeCommand : ISceneCommand
{
    private readonly SceneNode _node;

    public PlaceNodeCommand(SceneNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        _node = node;
    }

    public void Apply(EditorScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        scene.Add(_node);
    }

    public void Revert(EditorScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        scene.RemoveAt(scene.IndexOf(_node.Id));
    }

    public string Describe() => $"place \"{_node.Name}\" (#{_node.Id})";
}
