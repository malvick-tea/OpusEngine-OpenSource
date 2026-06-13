using System;
using System.Collections.Generic;

namespace Opus.Editor.Core;

/// <summary>
/// The undo / redo history for one document target. Executing a command applies it and clears the redo
/// branch (the standard linear-history invariant that keeps ids and the pseudo-code mirror consistent).
/// Mutates the bound <typeparamref name="TTarget"/> only through <see cref="IDocumentCommand{TTarget}"/>.
/// Generic so the scene graph and the animation graph reuse identical history mechanics.
/// </summary>
/// <typeparam name="TTarget">The mutable aggregate the commands edit.</typeparam>
public sealed class DocumentCommandStack<TTarget>
{
    private readonly List<IDocumentCommand<TTarget>> _undo = new();
    private readonly List<IDocumentCommand<TTarget>> _redo = new();
    private readonly TTarget _target;

    public DocumentCommandStack(TTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        _target = target;
    }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public int UndoDepth => _undo.Count;

    public int RedoDepth => _redo.Count;

    /// <summary>The applied commands, oldest first, as their <see cref="IDocumentCommand{TTarget}.Describe"/>
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

    public void Execute(IDocumentCommand<TTarget> command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Apply(_target);
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
        command.Revert(_target);
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
        command.Apply(_target);
        _undo.Add(command);
        return true;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
