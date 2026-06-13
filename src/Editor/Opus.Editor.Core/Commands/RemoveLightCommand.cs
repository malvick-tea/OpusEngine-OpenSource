using System;

namespace Opus.Editor.Core;

/// <summary>
/// Removes a light by id, capturing its value and list position on apply so <see cref="Revert"/> restores
/// it at exactly the same index — keeping light order (and therefore the pseudo-code mirror) stable across
/// undo, exactly mirroring <see cref="RemoveNodeCommand"/> for the scene's other element kind.
/// </summary>
public sealed class RemoveLightCommand : ISceneCommand
{
    private readonly SceneLightId _id;
    private SceneLight? _removed;
    private int _index = -1;

    public RemoveLightCommand(SceneLightId id)
    {
        _id = id;
    }

    public void Apply(EditorScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        _index = scene.IndexOfLight(_id);
        if (_index < 0)
        {
            throw new InvalidOperationException($"Cannot remove light #{_id}: not present.");
        }

        _removed = scene.RemoveLightAt(_index);
    }

    public void Revert(EditorScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (_removed is null)
        {
            throw new InvalidOperationException("Cannot revert a remove that never applied.");
        }

        scene.InsertLight(_index, _removed);
    }

    public string Describe() => $"remove light #{_id}";
}
