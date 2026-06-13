using System;

namespace Opus.Editor.Core;

/// <summary>
/// Replaces an existing state with an edited copy carrying the same id — the one command behind every
/// in-place state edit (rename, bind clip, toggle loop, change speed). Captures the prior state on
/// <see cref="Apply"/> so <see cref="Revert"/> restores it exactly.
/// </summary>
public sealed class ReplaceStateCommand : IDocumentCommand<AnimationGraph>
{
    private readonly AnimationState _next;
    private AnimationState? _previous;

    public ReplaceStateCommand(AnimationState next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    public void Apply(AnimationGraph target)
    {
        ArgumentNullException.ThrowIfNull(target);
        _previous = target.FindState(_next.Id)
            ?? throw new InvalidOperationException($"State #{_next.Id} does not exist.");
        target.ReplaceState(_next);
    }

    public void Revert(AnimationGraph target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (_previous is not null)
        {
            target.ReplaceState(_previous);
        }
    }

    public string Describe() => $"edit state \"{_next.Name}\" (#{_next.Id})";
}
