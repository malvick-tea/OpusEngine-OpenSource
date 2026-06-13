using System;
using System.Collections.Generic;

namespace Opus.Editor.Core;

/// <summary>
/// One open animation-graph document: the state graph, its undo / redo history, and the dirty flag, with
/// high-level authoring operations that build and execute the right <see cref="IDocumentCommand{TTarget}"/>.
/// Raises <see cref="Changed"/> after every mutation so the UI and the pseudo-code mirror refresh from one
/// place. Pure and GPU-free — the host renders a projection of this. Entry selection is explicit (the
/// validator flags a graph that has states but no entry), so each command stays one atomic undo step.
/// </summary>
public sealed class AnimationGraphEditor
{
    private readonly AnimationGraph _graph;
    private readonly DocumentCommandStack<AnimationGraph> _commands;

    public AnimationGraphEditor(string name = AnimationGraph.DefaultName)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        _graph = new AnimationGraph { Name = name };
        _commands = new DocumentCommandStack<AnimationGraph>(_graph);
    }

    /// <summary>Raised after any change to the graph, so observers refresh once.</summary>
    public event Action? Changed;

    public string Name => _graph.Name;

    public AnimationGraph Graph => _graph;

    public DocumentCommandStack<AnimationGraph> Commands => _commands;

    public AnimationStateId EntryState => _graph.EntryState;

    public bool IsDirty { get; private set; }

    public bool CanUndo => _commands.CanUndo;

    public bool CanRedo => _commands.CanRedo;

    /// <summary>Adds a state and returns its freshly allocated id.</summary>
    public AnimationStateId AddState(
        string name, string? clipRef = null, bool loop = true, float speed = AnimationState.DefaultSpeed)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        var id = _graph.AllocateId();
        _commands.Execute(new AddStateCommand(new AnimationState(id, name, clipRef, loop, speed)));
        MarkChanged();
        return id;
    }

    /// <summary>Wires a transition; returns false if either endpoint state does not exist.</summary>
    public bool AddTransition(AnimationStateId from, AnimationStateId to, string trigger, float blendSeconds = 0f)
    {
        ArgumentException.ThrowIfNullOrEmpty(trigger);
        if (!_graph.ContainsState(from) || !_graph.ContainsState(to))
        {
            return false;
        }

        _commands.Execute(new AddTransitionCommand(new AnimationTransition(from, to, trigger, blendSeconds)));
        MarkChanged();
        return true;
    }

    /// <summary>Sets the entry state; accepts <see cref="AnimationStateId.None"/> to clear it, but returns
    /// false for a non-existent state id.</summary>
    public bool SetEntryState(AnimationStateId id)
    {
        if (id.IsValid && !_graph.ContainsState(id))
        {
            return false;
        }

        _commands.Execute(new SetEntryStateCommand(id));
        MarkChanged();
        return true;
    }

    public bool RemoveState(AnimationStateId id)
    {
        if (!_graph.ContainsState(id))
        {
            return false;
        }

        _commands.Execute(new RemoveStateCommand(id));
        MarkChanged();
        return true;
    }

    /// <summary>Removes the transition with the given (from, to, trigger) identity; returns false when no
    /// such edge exists.</summary>
    public bool RemoveTransition(AnimationStateId from, AnimationStateId to, string trigger)
    {
        ArgumentException.ThrowIfNullOrEmpty(trigger);
        if (_graph.IndexOfTransition(from, to, trigger) < 0)
        {
            return false;
        }

        _commands.Execute(new RemoveTransitionCommand(from, to, trigger));
        MarkChanged();
        return true;
    }

    public bool RenameState(AnimationStateId id, string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        var state = _graph.FindState(id);
        if (state is null)
        {
            return false;
        }

        _commands.Execute(new ReplaceStateCommand(state.WithName(name)));
        MarkChanged();
        return true;
    }

    public bool BindClip(AnimationStateId id, string? clipRef)
    {
        var state = _graph.FindState(id);
        if (state is null)
        {
            return false;
        }

        _commands.Execute(new ReplaceStateCommand(state.WithClip(clipRef)));
        MarkChanged();
        return true;
    }

    public bool Undo()
    {
        if (!_commands.Undo())
        {
            return false;
        }

        MarkChanged();
        return true;
    }

    public bool Redo()
    {
        if (!_commands.Redo())
        {
            return false;
        }

        MarkChanged();
        return true;
    }

    /// <summary>Replaces the whole document with a loaded graph, resetting history and dirty state.</summary>
    public void LoadGraph(AnimationGraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _graph.Load(document);
        _commands.Clear();
        IsDirty = false;
        Changed?.Invoke();
    }

    public AnimationGraphDocument Snapshot() => _graph.Snapshot();

    public string ToPseudoCode() => AnimationGraphDslWriter.Write(_graph.Snapshot());

    public IReadOnlyList<AnimationGraphIssue> Validate() =>
        AnimationGraphValidator.Validate(_graph.Snapshot());

    public void MarkSaved() => IsDirty = false;

    private void MarkChanged()
    {
        IsDirty = true;
        Changed?.Invoke();
    }
}
