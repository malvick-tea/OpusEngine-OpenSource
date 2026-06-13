using System;

namespace Opus.Editor.Core;

/// <summary>
/// Replaces an existing light with a new value, capturing the previous value on apply so <see cref="Revert"/>
/// restores it. The reversible edit behind retuning a light's colour / intensity / position / direction /
/// range / cone (or its name) in place — its id and list position never change, so references and the
/// pseudo-code mirror stay stable.
/// </summary>
public sealed class SetLightCommand : ISceneCommand
{
    private readonly SceneLight _next;
    private SceneLight? _previous;

    public SetLightCommand(SceneLight next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    /// <summary>Creates the command with an explicit previous value already captured, so a coalesced gizmo
    /// drag (which previewed the scene directly across many frames) commits as a single reversible edit:
    /// undo restores <paramref name="previous"/>, redo reapplies <paramref name="next"/> — exactly
    /// mirroring <see cref="TransformNodeCommand"/> for nodes.</summary>
    public SetLightCommand(SceneLight previous, SceneLight next)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(next);
        _previous = previous;
        _next = next;
    }

    public void Apply(EditorScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        _previous ??= scene.FindLight(_next.Id)
            ?? throw new InvalidOperationException($"Cannot set light #{_next.Id}: not present.");
        scene.ReplaceLight(_next);
    }

    public void Revert(EditorScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (_previous is null)
        {
            throw new InvalidOperationException("Cannot revert a set that never applied.");
        }

        scene.ReplaceLight(_previous);
    }

    public string Describe() => $"set light #{_next.Id}";
}
