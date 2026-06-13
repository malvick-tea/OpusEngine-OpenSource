using System;

namespace Opus.Editor.Core;

/// <summary>
/// Sets the graph's entry state, capturing the previous entry so <see cref="Revert"/> restores it. The
/// command does not check that the target exists — <see cref="AnimationGraphValidator"/> reports an
/// unresolved entry — so an entry can be declared before the state it names is added.
/// </summary>
public sealed class SetEntryStateCommand : IDocumentCommand<AnimationGraph>
{
    private readonly AnimationStateId _next;
    private AnimationStateId _previous;

    public SetEntryStateCommand(AnimationStateId next)
    {
        _next = next;
    }

    public void Apply(AnimationGraph target)
    {
        ArgumentNullException.ThrowIfNull(target);
        _previous = target.EntryState;
        target.EntryState = _next;
    }

    public void Revert(AnimationGraph target)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.EntryState = _previous;
    }

    public string Describe() => $"set entry state #{_next}";
}
