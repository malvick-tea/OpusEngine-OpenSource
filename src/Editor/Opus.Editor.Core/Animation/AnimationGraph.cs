using System;
using System.Collections.Generic;

namespace Opus.Editor.Core;

/// <summary>
/// The mutable animation state graph behind one open document. Owns dense, deterministic id allocation,
/// the state store, and the transition list; animation commands are the only writers, so every change is
/// undoable and the pseudo-code mirror always reads a consistent snapshot. Pure and GPU-free.
/// </summary>
public sealed class AnimationGraph
{
    public const string DefaultName = "untitled";

    private readonly List<AnimationState> _states = new();
    private readonly List<AnimationTransition> _transitions = new();
    private int _nextId = 1;

    public string Name { get; set; } = DefaultName;

    public AnimationStateId EntryState { get; set; } = AnimationStateId.None;

    public IReadOnlyList<AnimationState> States => _states;

    public IReadOnlyList<AnimationTransition> Transitions => _transitions;

    public int StateCount => _states.Count;

    public int TransitionCount => _transitions.Count;

    /// <summary>Allocates the next dense id without inserting a state. Allocation never rolls back on
    /// undo, so a later add can never collide with an undone-then-redone state.</summary>
    public AnimationStateId AllocateId() => new(_nextId++);

    public bool ContainsState(AnimationStateId id) => IndexOfState(id) >= 0;

    public int IndexOfState(AnimationStateId id)
    {
        for (int i = 0; i < _states.Count; i++)
        {
            if (_states[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    public AnimationState? FindState(AnimationStateId id)
    {
        int index = IndexOfState(id);
        return index >= 0 ? _states[index] : null;
    }

    public AnimationState? FindStateByName(string name)
    {
        foreach (var state in _states)
        {
            if (string.Equals(state.Name, name, StringComparison.Ordinal))
            {
                return state;
            }
        }

        return null;
    }

    public void AddState(AnimationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (ContainsState(state.Id))
        {
            throw new InvalidOperationException($"State #{state.Id} already exists.");
        }

        _states.Add(state);
    }

    public void InsertState(int index, AnimationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (index < 0 || index > _states.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Insert index out of range.");
        }

        if (ContainsState(state.Id))
        {
            throw new InvalidOperationException($"State #{state.Id} already exists.");
        }

        _states.Insert(index, state);
    }

    public AnimationState RemoveStateAt(int index)
    {
        if (index < 0 || index >= _states.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Remove index out of range.");
        }

        var state = _states[index];
        _states.RemoveAt(index);
        return state;
    }

    public void ReplaceState(AnimationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        int index = IndexOfState(state.Id);
        if (index < 0)
        {
            throw new InvalidOperationException($"State #{state.Id} does not exist.");
        }

        _states[index] = state;
    }

    /// <summary>The index of the first transition matching the (from, to, trigger) identity, or -1 when no
    /// such edge exists. Transitions are identified by their endpoints and trigger, not a surrogate id.</summary>
    public int IndexOfTransition(AnimationStateId from, AnimationStateId to, string trigger)
    {
        for (int i = 0; i < _transitions.Count; i++)
        {
            var transition = _transitions[i];
            if (transition.From == from && transition.To == to &&
                string.Equals(transition.Trigger, trigger, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    public void AddTransition(AnimationTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        _transitions.Add(transition);
    }

    public void InsertTransition(int index, AnimationTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        if (index < 0 || index > _transitions.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Insert index out of range.");
        }

        _transitions.Insert(index, transition);
    }

    public AnimationTransition RemoveTransitionAt(int index)
    {
        if (index < 0 || index >= _transitions.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Remove index out of range.");
        }

        var transition = _transitions[index];
        _transitions.RemoveAt(index);
        return transition;
    }

    public AnimationGraphDocument Snapshot() => new(
        Name, EntryState, new List<AnimationState>(_states), new List<AnimationTransition>(_transitions));

    public void Load(AnimationGraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _states.Clear();
        _states.AddRange(document.States);
        _transitions.Clear();
        _transitions.AddRange(document.Transitions);
        Name = document.Name;
        EntryState = document.EntryState;
        _nextId = NextIdAfter(document.States);
    }

    private static int NextIdAfter(IReadOnlyList<AnimationState> states)
    {
        int max = 0;
        foreach (var state in states)
        {
            if (state.Id.Value > max)
            {
                max = state.Id.Value;
            }
        }

        return max + 1;
    }
}
