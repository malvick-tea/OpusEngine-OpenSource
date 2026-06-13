using System;
using System.Collections.Generic;

namespace Opus.Editor.Core;

/// <summary>
/// The mutable, densely-id-allocated store behind one kind of scene element (nodes, lights, …). Owns the
/// ordered element list and a monotonic id counter that never rolls back, so a replayed command sequence
/// reproduces identical ids — the invariant undo / redo and the pseudo-code mirror rely on.
/// <see cref="EditorScene"/> composes one store per element kind and exposes a typed facade over each, which
/// is why the node and light surfaces share this code instead of duplicating it. The element supplies its
/// own id through the <c>idOf</c> projection; the store mints new ids through <c>idFactory</c>. Pure and
/// GPU-free.
/// </summary>
internal sealed class SceneElementStore<TId, TElement>
    where TId : struct, IEquatable<TId>, ISceneElementId
    where TElement : class
{
    private readonly List<TElement> _elements = new();
    private readonly Func<TElement, TId> _idOf;
    private readonly Func<int, TId> _idFactory;
    private readonly string _elementLabel;
    private int _nextId = 1;

    public SceneElementStore(Func<TElement, TId> idOf, Func<int, TId> idFactory, string elementLabel)
    {
        ArgumentNullException.ThrowIfNull(idOf);
        ArgumentNullException.ThrowIfNull(idFactory);
        ArgumentException.ThrowIfNullOrEmpty(elementLabel);
        _idOf = idOf;
        _idFactory = idFactory;
        _elementLabel = elementLabel;
    }

    public IReadOnlyList<TElement> Elements => _elements;

    public int Count => _elements.Count;

    /// <summary>Allocates the next dense id without inserting an element. Allocation never rolls back on
    /// undo, so a later add can never collide with an undone-then-redone element.</summary>
    public TId AllocateId() => _idFactory(_nextId++);

    public bool Contains(TId id) => IndexOf(id) >= 0;

    public int IndexOf(TId id)
    {
        for (int i = 0; i < _elements.Count; i++)
        {
            if (_idOf(_elements[i]).Equals(id))
            {
                return i;
            }
        }

        return -1;
    }

    public TElement? Find(TId id)
    {
        int index = IndexOf(id);
        return index >= 0 ? _elements[index] : null;
    }

    public void Add(TElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (Contains(_idOf(element)))
        {
            throw new InvalidOperationException($"{_elementLabel} #{_idOf(element)} already exists.");
        }

        _elements.Add(element);
    }

    public void Insert(int index, TElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (index < 0 || index > _elements.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Insert index out of range.");
        }

        if (Contains(_idOf(element)))
        {
            throw new InvalidOperationException($"{_elementLabel} #{_idOf(element)} already exists.");
        }

        _elements.Insert(index, element);
    }

    public TElement RemoveAt(int index)
    {
        if (index < 0 || index >= _elements.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Remove index out of range.");
        }

        var element = _elements[index];
        _elements.RemoveAt(index);
        return element;
    }

    public void Replace(TElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        int index = IndexOf(_idOf(element));
        if (index < 0)
        {
            throw new InvalidOperationException($"{_elementLabel} #{_idOf(element)} does not exist.");
        }

        _elements[index] = element;
    }

    /// <summary>Replaces the store contents with <paramref name="elements"/> and resets the id counter past
    /// the highest loaded id, so the next allocation never collides with a loaded element.</summary>
    public void LoadFrom(IReadOnlyList<TElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        _elements.Clear();
        _elements.AddRange(elements);
        _nextId = NextIdAfter(elements);
    }

    private int NextIdAfter(IReadOnlyList<TElement> elements)
    {
        int max = 0;
        foreach (var element in elements)
        {
            int value = _idOf(element).Value;
            if (value > max)
            {
                max = value;
            }
        }

        return max + 1;
    }
}
