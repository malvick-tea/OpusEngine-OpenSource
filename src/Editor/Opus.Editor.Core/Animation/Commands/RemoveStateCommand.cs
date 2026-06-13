using System;
using System.Collections.Generic;

namespace Opus.Editor.Core;

/// <summary>
/// Removes a state and cascades to every transition that touches it, capturing the state, its index, the
/// removed edges with their indices, and whether it was the entry state so <see cref="Revert"/> restores
/// the graph exactly. Cascading keeps the graph free of dangling transitions after a delete.
/// </summary>
public sealed class RemoveStateCommand : IDocumentCommand<AnimationGraph>
{
    private readonly AnimationStateId _id;
    private readonly List<(int Index, AnimationTransition Transition)> _removedTransitions = new();
    private AnimationState? _removedState;
    private int _stateIndex = -1;
    private bool _wasEntry;

    public RemoveStateCommand(AnimationStateId id)
    {
        _id = id;
    }

    public void Apply(AnimationGraph target)
    {
        ArgumentNullException.ThrowIfNull(target);
        _stateIndex = target.IndexOfState(_id);
        if (_stateIndex < 0)
        {
            throw new InvalidOperationException($"State #{_id} does not exist.");
        }

        _removedTransitions.Clear();
        for (int i = target.TransitionCount - 1; i >= 0; i--)
        {
            var transition = target.Transitions[i];
            if (transition.From == _id || transition.To == _id)
            {
                _removedTransitions.Add((i, transition));
                target.RemoveTransitionAt(i);
            }
        }

        _wasEntry = target.EntryState == _id;
        _removedState = target.RemoveStateAt(_stateIndex);
        if (_wasEntry)
        {
            target.EntryState = AnimationStateId.None;
        }
    }

    public void Revert(AnimationGraph target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (_removedState is null)
        {
            return;
        }

        target.InsertState(_stateIndex, _removedState);

        // Captured back-to-front, so iterate in reverse to re-insert at ascending original indices.
        for (int i = _removedTransitions.Count - 1; i >= 0; i--)
        {
            var (index, transition) = _removedTransitions[i];
            target.InsertTransition(index, transition);
        }

        if (_wasEntry)
        {
            target.EntryState = _id;
        }
    }

    public string Describe() => $"remove state #{_id}";
}
