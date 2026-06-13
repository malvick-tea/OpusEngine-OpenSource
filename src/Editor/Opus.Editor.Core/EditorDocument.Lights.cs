using System;
using System.Globalization;

namespace Opus.Editor.Core;

/// <summary>Light authoring operations of the <see cref="EditorDocument"/> aggregate: add, remove, preview /
/// commit a drag, duplicate, and retune a single light. Lights are a separate element kind from nodes with
/// their own dense id sequence, so these never touch the node-only <see cref="Selection"/> view directly —
/// they go through the shared selection set like the node ops.</summary>
public sealed partial class EditorDocument
{
    /// <summary>Adds <paramref name="light"/> to the scene with a freshly allocated id and returns that id.
    /// The spec's own id is ignored (callers pass <see cref="SceneLightId.None"/>); one undoable edit. Lights
    /// are a separate element kind from nodes, so this never touches the node <see cref="Selection"/>.</summary>
    public SceneLightId AddLight(SceneLight light)
    {
        ArgumentNullException.ThrowIfNull(light);
        ArgumentException.ThrowIfNullOrWhiteSpace(light.Name);
        var id = _scene.AllocateLightId();
        _commands.Execute(new AddLightCommand(light.WithId(id)));
        MarkChanged();
        return id;
    }

    /// <summary>Adds a new point light at <paramref name="position"/>, named "light N" from its freshly
    /// allocated id so a window-created light never needs a name prompt, selects it, and returns the id.
    /// One undoable edit — the window's "+ Light" button and L key land here.</summary>
    public SceneLightId AddNewPointLight(Float3 position)
    {
        var id = _scene.AllocateLightId();
        string name = string.Create(CultureInfo.InvariantCulture, $"light {id.Value}");
        _commands.Execute(new AddLightCommand(SceneLight.CreatePoint(name).WithId(id) with { Position = position }));
        _selection.SetLight(id);
        MarkChanged();
        return id;
    }

    public bool RemoveLight(SceneLightId id)
    {
        if (!_scene.ContainsLight(id))
        {
            return false;
        }

        _commands.Execute(new RemoveLightCommand(id));
        _selection.Remove(SceneElementRef.Light(id));
        MarkChanged();
        return true;
    }

    /// <summary>Sets a light's value directly for a live gizmo-drag preview — marks the document dirty and
    /// raises <see cref="Changed"/>, but records NO undo step. Pair with <see cref="CommitLight"/> on drag
    /// end so the whole gesture collapses to one reversible edit (the light twin of
    /// <see cref="PreviewNodeTransform"/>). No-op (false) for a missing light.</summary>
    public bool PreviewLight(SceneLight light)
    {
        ArgumentNullException.ThrowIfNull(light);
        if (!_scene.ContainsLight(light.Id))
        {
            return false;
        }

        _scene.ReplaceLight(light);
        MarkChanged();
        return true;
    }

    /// <summary>Commits a coalesced light drag as a single reversible edit: one command whose undo restores
    /// <paramref name="from"/> and whose redo reapplies <paramref name="to"/>, regardless of how many
    /// preview frames the drag spanned (the light twin of <see cref="CommitNodeTransform"/>). No-op (false)
    /// for a missing light.</summary>
    public bool CommitLight(SceneLight from, SceneLight to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        if (!_scene.ContainsLight(to.Id))
        {
            return false;
        }

        _commands.Execute(new SetLightCommand(from, to));
        MarkChanged();
        return true;
    }

    /// <summary>Clones <paramref name="id"/> as a new selected light — same kind and parameters, the name
    /// suffixed with " copy", and the position either <paramref name="atPosition"/> or offset one metre
    /// along X (mirroring <see cref="DuplicateNode"/>). One undoable edit. Returns the new light's id, or
    /// <see cref="SceneLightId.None"/> when <paramref name="id"/> is not in the scene.</summary>
    public SceneLightId DuplicateLight(SceneLightId id, Float3? atPosition = null)
    {
        var source = _scene.FindLight(id);
        if (source is null)
        {
            return SceneLightId.None;
        }

        var newId = _scene.AllocateLightId();
        var position = atPosition ?? OffsetForCopy(source.Position);
        var copy = source.WithId(newId).WithName(source.Name + DuplicateNameSuffix) with { Position = position };
        _commands.Execute(new AddLightCommand(copy));
        _selection.SetLight(newId);
        MarkChanged();
        return newId;
    }

    /// <summary>Replaces the light carrying <paramref name="light"/>'s id with the new value as one undoable
    /// edit, keeping its id and list position. No-op (false) when no light has that id.</summary>
    public bool SetLight(SceneLight light)
    {
        ArgumentNullException.ThrowIfNull(light);
        ArgumentException.ThrowIfNullOrWhiteSpace(light.Name);
        if (!_scene.ContainsLight(light.Id))
        {
            return false;
        }

        _commands.Execute(new SetLightCommand(light));
        MarkChanged();
        return true;
    }
}
