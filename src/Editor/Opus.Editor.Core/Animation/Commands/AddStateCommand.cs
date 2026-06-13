using System;

namespace Opus.Editor.Core;

/// <summary>
/// Adds a fully-formed state (its id already allocated from the graph) to the end of the state list.
/// <see cref="Revert"/> removes it again; redo re-adds the identical state, so its id is stable across the
/// undo / redo cycle.
/// </summary>
public sealed class AddStateCommand : IDocumentCommand<AnimationGraph>
{
    private readonly AnimationState _state;

    public AddStateCommand(AnimationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
    }

    public void Apply(AnimationGraph target)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.AddState(_state);
    }

    public void Revert(AnimationGraph target)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.RemoveStateAt(target.IndexOfState(_state.Id));
    }

    public string Describe() => $"add state \"{_state.Name}\" (#{_state.Id})";
}
