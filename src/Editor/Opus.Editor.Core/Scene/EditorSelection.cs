using System.Collections.Generic;

namespace Opus.Editor.Core;

/// <summary>
/// The editor's current selection: an ordered set of element references over one scene, the last entry being
/// the primary (the element the inspector, gizmo, and status line address). Selection is UI state, not
/// document state — it is never undoable and never dirties the document — so it lives in its own object that
/// validates every member against the scene (a ref naming a missing element is dropped) and clamps itself
/// when an undo / redo / remove takes an element out. The owning <see cref="EditorDocument"/> raises its
/// change event after mutating this; the set itself is silent.
/// </summary>
internal sealed class EditorSelection
{
    private readonly EditorScene _scene;
    private readonly List<SceneElementRef> _elements = new();

    public EditorSelection(EditorScene scene)
    {
        _scene = scene;
    }

    /// <summary>Every selected element in selection order; the last entry is the <see cref="Primary"/>.</summary>
    public IReadOnlyList<SceneElementRef> Elements => _elements;

    /// <summary>The newest member of the set, or <see cref="SceneElementRef.None"/> when empty.</summary>
    public SceneElementRef Primary => _elements.Count > 0 ? _elements[^1] : SceneElementRef.None;

    /// <summary>Toggles one element's membership (a Ctrl+click): an unselected element joins and becomes the
    /// primary; an already-selected member leaves. An invalid ref or one naming a missing element is ignored.
    /// Returns true when the set actually changed, so the caller raises its change event only then.</summary>
    public bool Toggle(SceneElementRef element)
    {
        if (!element.IsValid)
        {
            return false;
        }

        if (_elements.Remove(element))
        {
            return true;
        }

        if (Exists(element))
        {
            _elements.Add(element);
            return true;
        }

        return false;
    }

    /// <summary>Replaces the set with <paramref name="elements"/> — or, when <paramref name="additive"/>,
    /// unions them into the existing set. Invalid refs, refs naming missing elements, and duplicates are
    /// dropped; the given order is kept, so the last surviving element becomes the primary.</summary>
    public void Replace(IReadOnlyList<SceneElementRef> elements, bool additive)
    {
        if (!additive)
        {
            _elements.Clear();
        }

        foreach (var element in elements)
        {
            if (element.IsValid && !_elements.Contains(element) && Exists(element))
            {
                _elements.Add(element);
            }
        }
    }

    /// <summary>Sets the set to exactly one node (or clears it for an invalid id) — a plain node click.</summary>
    public void SetNode(SceneNodeId id)
    {
        _elements.Clear();
        if (id.IsValid)
        {
            _elements.Add(SceneElementRef.Node(id));
        }
    }

    /// <summary>Sets the set to exactly one light (or clears it for an invalid id) — a plain light click.</summary>
    public void SetLight(SceneLightId id)
    {
        _elements.Clear();
        if (id.IsValid)
        {
            _elements.Add(SceneElementRef.Light(id));
        }
    }

    /// <summary>Replaces the set with exactly <paramref name="elements"/>, order kept — the freshly created
    /// elements of a duplicate / paste / ungroup, which are already known to exist.</summary>
    public void SetAll(IReadOnlyList<SceneElementRef> elements)
    {
        _elements.Clear();
        _elements.AddRange(elements);
    }

    /// <summary>Empties the set (a scene load).</summary>
    public void Clear() => _elements.Clear();

    /// <summary>Drops one element from the set if present (the element was just removed from the scene).</summary>
    public void Remove(SceneElementRef element) => _elements.Remove(element);

    /// <summary>Drops every member whose element is no longer in the scene — after an undo / redo or a group
    /// remove the set never points at an element that is gone.</summary>
    public void Clamp() => _elements.RemoveAll(element => !Exists(element));

    private bool Exists(SceneElementRef element) =>
        element.IsNode ? _scene.Contains(element.AsNode) : _scene.ContainsLight(element.AsLight);
}
