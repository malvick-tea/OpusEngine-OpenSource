using System;
using System.Collections.Generic;

namespace Opus.Editor.Core;

/// <summary>
/// The undo / redo history for one document. Executing a command applies it and clears the redo branch
/// (the standard linear-history invariant that keeps node ids and the pseudo-code mirror consistent).
/// Pure: it mutates the bound <see cref="EditorScene"/> only through <see cref="ISceneCommand"/>.
/// </summary>
public sealed class EditorCommandStack
{
    private readonly List<ISceneCommand> _undo = new();
    private readonly List<ISceneCommand> _redo = new();
    private readonly EditorScene _scene;

    public EditorCommandStack(EditorScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        _scene = scene;
    }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public int UndoDepth => _undo.Count;

    public int RedoDepth => _redo.Count;

    /// <summary>The applied commands, oldest first, as their <see cref="ISceneCommand.Describe"/>
    /// labels — the action history a tester or developer reads.</summary>
    public IReadOnlyList<string> History
    {
        get
        {
            var labels = new List<string>(_undo.Count);
            foreach (var command in _undo)
            {
                labels.Add(command.Describe());
            }

            return labels;
        }
    }

    public void Execute(ISceneCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Apply(_scene);
        _undo.Add(command);
        _redo.Clear();
    }

    public bool Undo()
    {
        if (_undo.Count == 0)
        {
            return false;
        }

        var command = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        command.Revert(_scene);
        _redo.Add(command);
        return true;
    }

    public bool Redo()
    {
        if (_redo.Count == 0)
        {
            return false;
        }

        var command = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        command.Apply(_scene);
        _undo.Add(command);
        return true;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
