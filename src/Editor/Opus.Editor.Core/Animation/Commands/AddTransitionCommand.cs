using System;

namespace Opus.Editor.Core;

/// <summary>
/// Appends a transition to the graph. <see cref="Revert"/> removes the last transition — valid because
/// linear history guarantees this command's transition is the most recently added when it is undone, and
/// redo re-appends the identical edge.
/// </summary>
public sealed class AddTransitionCommand : IDocumentCommand<AnimationGraph>
{
    private readonly AnimationTransition _transition;

    public AddTransitionCommand(AnimationTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        _transition = transition;
    }

    public void Apply(AnimationGraph target)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.AddTransition(_transition);
    }

    public void Revert(AnimationGraph target)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.RemoveTransitionAt(target.TransitionCount - 1);
    }

    public string Describe() =>
        $"add transition #{_transition.From} -> #{_transition.To} on \"{_transition.Trigger}\"";
}
