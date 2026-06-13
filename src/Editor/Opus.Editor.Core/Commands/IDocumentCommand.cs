namespace Opus.Editor.Core;

/// <summary>
/// One reversible mutation of a document target of type <typeparamref name="TTarget"/>. The
/// <see cref="DocumentCommandStack{TTarget}"/> calls <see cref="Apply"/> to perform (and re-perform on
/// redo) and <see cref="Revert"/> to undo; an implementation captures whatever inverse state it needs
/// during <see cref="Apply"/>. <see cref="Describe"/> yields a short, human label for the action history.
/// Commands are the only writers of target state — that is what makes undo / redo and the pseudo-code
/// mirror reliable. The generic form lets the scene graph and the animation graph share one stack.
/// </summary>
/// <typeparam name="TTarget">The mutable aggregate the command edits.</typeparam>
public interface IDocumentCommand<in TTarget>
{
    void Apply(TTarget target);

    void Revert(TTarget target);

    string Describe();
}
