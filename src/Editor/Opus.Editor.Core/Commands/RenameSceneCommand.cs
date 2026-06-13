using System;

namespace Opus.Editor.Core;

/// <summary>
/// Renames the scene document itself (the name the mirror's <c>scene "…"</c> header and the OS window
/// title carry), capturing the previous name on the first apply so <see cref="Revert"/> (and a later redo)
/// round-trips exactly.
/// </summary>
public sealed class RenameSceneCommand : ISceneCommand
{
    private readonly string _next;
    private string? _previous;

    public RenameSceneCommand(string next)
    {
        ArgumentException.ThrowIfNullOrEmpty(next);
        _next = next;
    }

    public void Apply(EditorScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        _previous ??= scene.Name;
        scene.Name = _next;
    }

    public void Revert(EditorScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        scene.Name = _previous ?? scene.Name;
    }

    public string Describe() => $"rename scene to \"{_next}\"";
}
