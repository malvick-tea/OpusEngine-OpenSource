namespace Opus.Editor.Core;

/// <summary>
/// One reversible mutation of an <see cref="EditorScene"/>. The command stack calls <see cref="Apply"/>
/// to perform (and re-perform on redo) and <see cref="Revert"/> to undo; an implementation captures
/// whatever inverse state it needs during <see cref="Apply"/>. <see cref="Describe"/> yields a short,
/// human label for the action history. Commands are the only writers of scene state — that is what makes
/// undo / redo and the pseudo-code mirror reliable.
/// </summary>
public interface ISceneCommand
{
    void Apply(EditorScene scene);

    void Revert(EditorScene scene);

    string Describe();
}
