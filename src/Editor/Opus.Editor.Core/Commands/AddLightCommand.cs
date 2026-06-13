using System;

namespace Opus.Editor.Core;

/// <summary>
/// Adds a fully-formed light (its id already allocated from the scene) to the end of the light list.
/// <see cref="Revert"/> removes it again; redo re-adds the identical light, so its id is stable across the
/// undo / redo cycle — exactly mirroring <see cref="PlaceNodeCommand"/> for the scene's other element kind.
/// </summary>
public sealed class AddLightCommand : ISceneCommand
{
    private readonly SceneLight _light;

    public AddLightCommand(SceneLight light)
    {
        ArgumentNullException.ThrowIfNull(light);
        _light = light;
    }

    public void Apply(EditorScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        scene.AddLight(_light);
    }

    public void Revert(EditorScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        scene.RemoveLightAt(scene.IndexOfLight(_light.Id));
    }

    public string Describe() => $"add light \"{_light.Name}\" (#{_light.Id})";
}
