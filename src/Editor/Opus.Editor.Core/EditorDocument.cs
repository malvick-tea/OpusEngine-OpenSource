using System;
using System.Collections.Generic;

namespace Opus.Editor.Core;

/// <summary>
/// One open editor document: the scene graph, its undo / redo history, the current selection, and the
/// dirty flag, with high-level authoring operations that build and execute the right
/// <see cref="ISceneCommand"/>. Raises <see cref="Changed"/> after every mutation so the UI and the
/// pseudo-code mirror refresh from one place. Pure and GPU-free — the host renders a projection of this.
///
/// The document is the single transactional authority over its scene: every authoring op validates, builds
/// commands, executes them through one <see cref="EditorCommandStack"/>, updates the selection, and marks
/// the document changed. That shared transactional spine is the cohesion, so the type is one aggregate split
/// across partial files by element concern (nodes, lights, multi-element groups, hierarchy) to stay within
/// the file cap rather than fragmenting the spine into separate mutators. The pure pieces that can stand
/// alone are extracted: the selection set (<see cref="EditorSelection"/>) and the grouping math
/// (<see cref="SceneGroupingPlanner"/>).
/// </summary>
public sealed partial class EditorDocument
{
    private const string DuplicateNameSuffix = " copy";
    private const float DuplicateOffsetMeters = 1f;

    private readonly EditorScene _scene;
    private readonly EditorCommandStack _commands;
    private readonly EditorSelection _selection;

    public EditorDocument(string name = EditorScene.DefaultName)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        _scene = new EditorScene { Name = name };
        _commands = new EditorCommandStack(_scene);
        _selection = new EditorSelection(_scene);
    }

    /// <summary>Raised after any change to the document or selection, so observers refresh once.</summary>
    public event Action? Changed;

    public string Name => _scene.Name;

    public EditorScene Scene => _scene;

    public EditorCommandStack Commands => _commands;

    public bool IsDirty { get; private set; }

    public bool CanUndo => _commands.CanUndo;

    public bool CanRedo => _commands.CanRedo;

    /// <summary>The primary selected node, or <see cref="SceneNodeId.None"/> — including when the primary
    /// selection is a light. A derived view of the selection set, kept for the node-only consumers.</summary>
    public SceneNodeId Selection => SelectedElement.AsNode;

    /// <summary>The primary selected light, or <see cref="SceneLightId.None"/> — including when the primary
    /// selection is a node. A derived view of the selection set, like <see cref="Selection"/>.</summary>
    public SceneLightId LightSelection => SelectedElement.AsLight;

    /// <summary>The primary selection as a kind-discriminated element reference — the element the
    /// inspector, gizmo, and status line address. The newest member of the selection set.</summary>
    public SceneElementRef SelectedElement => _selection.Primary;

    /// <summary>Every selected element in selection order; the last entry is the primary
    /// (<see cref="SelectedElement"/>). A plain click yields a one-entry list, so single-selection
    /// consumers read the same shape they always did; <see cref="ToggleSelect"/> grows the set.</summary>
    public IReadOnlyList<SceneElementRef> SelectedElements => _selection.Elements;

    /// <summary>Toggles one element's membership in the selection set (a Ctrl+click): an unselected
    /// element joins and becomes the primary; an already-selected member leaves, the primary falling back
    /// to the most recent remaining one. An invalid ref or one naming a missing element is ignored.
    /// Selection is UI state, not document state — never undoable, never dirties the document.</summary>
    public void ToggleSelect(SceneElementRef element)
    {
        if (_selection.Toggle(element))
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Replaces the selection set with <paramref name="elements"/> — the marquee (box) select
    /// landing point. With <paramref name="additive"/> the elements union into the existing set instead
    /// (Ctrl held while boxing), so a second box grows the selection rather than replacing it. Invalid
    /// refs, refs naming missing elements, and duplicates are dropped; the given order is kept, so the
    /// last surviving element becomes the primary. Selection is UI state, not document state — never
    /// undoable, never dirties the document.</summary>
    public void SelectElements(IReadOnlyList<SceneElementRef> elements, bool additive = false)
    {
        ArgumentNullException.ThrowIfNull(elements);
        _selection.Replace(elements, additive);
        Changed?.Invoke();
    }

    /// <summary>Selects a single node (the outliner / viewport node click), clearing any prior selection.</summary>
    public void Select(SceneNodeId id)
    {
        _selection.SetNode(id);
        Changed?.Invoke();
    }

    /// <summary>Selects a light, clearing any node selection (one selected element across both kinds).</summary>
    public void SelectLight(SceneLightId id)
    {
        _selection.SetLight(id);
        Changed?.Invoke();
    }

    public bool Undo()
    {
        if (!_commands.Undo())
        {
            return false;
        }

        _selection.Clamp();
        MarkChanged();
        return true;
    }

    public bool Redo()
    {
        if (!_commands.Redo())
        {
            return false;
        }

        _selection.Clamp();
        MarkChanged();
        return true;
    }

    /// <summary>Renames the scene document itself as one undoable edit — the mirror's header and the OS
    /// window title follow. False when the name is unchanged.</summary>
    public bool RenameDocument(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (string.Equals(_scene.Name, name, StringComparison.Ordinal))
        {
            return false;
        }

        _commands.Execute(new RenameSceneCommand(name));
        MarkChanged();
        return true;
    }

    /// <summary>Replaces the whole document with a loaded scene, resetting history and dirty state.</summary>
    public void LoadScene(EditorSceneDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _scene.Load(document);
        _commands.Clear();
        _selection.Clear();
        IsDirty = false;
        Changed?.Invoke();
    }

    public EditorSceneDocument Snapshot() => _scene.Snapshot();

    public string ToPseudoCode() => SceneDslWriter.Write(_scene.Snapshot());

    public void MarkSaved() => IsDirty = false;

    /// <summary>One command stays itself; several wrap into a composite — so a group of one keeps the
    /// plain command's history label and a real group undoes as a single step.</summary>
    private static ISceneCommand Unwrap(IReadOnlyList<ISceneCommand> commands) =>
        commands.Count == 1 ? commands[0] : new CompositeSceneCommand(commands);

    /// <summary>The listed elements with duplicates dropped, preserving order — the group APIs must never
    /// build two commands for one element (the second remove would throw mid-composite).</summary>
    private static IEnumerable<SceneElementRef> Distinct(IReadOnlyList<SceneElementRef> elements)
    {
        var seen = new HashSet<SceneElementRef>();
        foreach (var element in elements)
        {
            if (seen.Add(element))
            {
                yield return element;
            }
        }
    }

    private static Float3 OffsetForCopy(Float3 position) =>
        position with { X = position.X + DuplicateOffsetMeters };

    /// <summary>The node id plus every descendant id, so a removal takes the whole subtree.</summary>
    private List<SceneNodeId> SubtreeIds(SceneNodeId id)
    {
        var ids = new List<SceneNodeId> { id };
        ids.AddRange(SceneHierarchy.DescendantsOf(_scene.Nodes, id));
        return ids;
    }

    private void MarkChanged()
    {
        IsDirty = true;
        Changed?.Invoke();
    }
}
