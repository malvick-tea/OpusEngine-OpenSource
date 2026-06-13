using System;

namespace Opus.Editor.Core;

/// <summary>
/// Removes the first transition matching a (from, to, trigger) identity, capturing it and its index so
/// <see cref="Revert"/> re-inserts the identical edge at its original position. Unlike removing a state,
/// this touches only the one edge — states and other transitions are untouched.
/// </summary>
public sealed class RemoveTransitionCommand : IDocumentCommand<AnimationGraph>
{
    private readonly AnimationStateId _from;
    private readonly AnimationStateId _to;
    private readonly string _trigger;
    private AnimationTransition? _removed;
    private int _index = -1;

    public RemoveTransitionCommand(AnimationStateId from, AnimationStateId to, string trigger)
    {
        ArgumentException.ThrowIfNullOrEmpty(trigger);
        _from = from;
        _to = to;
        _trigger = trigger;
    }

    public void Apply(AnimationGraph target)
    {
        ArgumentNullException.ThrowIfNull(target);
        _index = target.IndexOfTransition(_from, _to, _trigger);
        if (_index < 0)
        {
            throw new InvalidOperationException(
                $"No transition #{_from} -> #{_to} on \"{_trigger}\" exists.");
        }

        _removed = target.RemoveTransitionAt(_index);
    }

    public void Revert(AnimationGraph target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (_removed is null)
        {
            return;
        }

        target.InsertTransition(_index, _removed);
    }

    public string Describe() => $"remove transition #{_from} -> #{_to} on \"{_trigger}\"";
}
