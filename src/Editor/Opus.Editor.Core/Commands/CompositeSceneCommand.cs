using System;
using System.Collections.Generic;

namespace Opus.Editor.Core;

/// <summary>
/// Applies an ordered list of scene commands as one undoable history entry, so a group edit over a
/// multi-selection (delete / hide / duplicate) collapses to a single undo step. <see cref="Apply"/> runs
/// the children in order; <see cref="Revert"/> unwinds them in reverse, so an index-capturing child (a
/// remove's restore position) reverts against exactly the scene state it captured.
/// </summary>
public sealed class CompositeSceneCommand : ISceneCommand
{
    private readonly IReadOnlyList<ISceneCommand> _children;

    public CompositeSceneCommand(IReadOnlyList<ISceneCommand> children)
    {
        ArgumentNullException.ThrowIfNull(children);
        if (children.Count == 0)
        {
            throw new ArgumentException("A composite command needs at least one child.", nameof(children));
        }

        _children = children;
    }

    public void Apply(EditorScene scene)
    {
        foreach (var child in _children)
        {
            child.Apply(scene);
        }
    }

    public void Revert(EditorScene scene)
    {
        for (int i = _children.Count - 1; i >= 0; i--)
        {
            _children[i].Revert(scene);
        }
    }

    public string Describe()
    {
        var labels = new string[_children.Count];
        for (int i = 0; i < _children.Count; i++)
        {
            labels[i] = _children[i].Describe();
        }

        return $"group({_children.Count}): {string.Join(", ", labels)}";
    }
}
