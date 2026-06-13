using System;
using System.Collections.Generic;

namespace Opus.Editor.Core;

/// <summary>
/// Pure, allocation-light queries over a node list's parent / child structure: which nodes are roots, the
/// direct children and full descendants of a node, whether a re-parent would form a cycle, and a node's
/// depth. Every walk is cycle-safe (a malformed scene file with a parent cycle never loops forever) and
/// dangling-parent tolerant (a node whose <see cref="SceneNode.ParentId"/> names a node not present is
/// treated as a root). Engine-neutral and GPU-free — the DSL mirror, the cascade-delete path, and the
/// re-parent validation all read the tree through this one place.
/// </summary>
public static class SceneHierarchy
{
    /// <summary>True when <paramref name="node"/> sits at the top of the tree: it has no parent, or its
    /// parent id names a node not present in <paramref name="nodes"/> (a dangling reference). A flat scene —
    /// no node carrying a parent — makes every node a root.</summary>
    public static bool IsRoot(IReadOnlyList<SceneNode> nodes, SceneNode node)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(node);
        return node.ParentId is not { } parent || !Exists(nodes, parent);
    }

    /// <summary>The direct children of <paramref name="parentId"/>, in document order.</summary>
    public static IEnumerable<SceneNode> ChildrenOf(IReadOnlyList<SceneNode> nodes, SceneNodeId parentId)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        foreach (var node in nodes)
        {
            if (node.ParentId is { } parent && parent == parentId)
            {
                yield return node;
            }
        }
    }

    /// <summary>Every descendant id of <paramref name="id"/> — children, grandchildren, and so on — in
    /// document pre-order, excluding <paramref name="id"/> itself. Cycle-safe via a visited set, so a
    /// malformed cyclic file yields each reachable node once and terminates.</summary>
    public static IReadOnlyList<SceneNodeId> DescendantsOf(IReadOnlyList<SceneNode> nodes, SceneNodeId id)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var result = new List<SceneNodeId>();
        var visited = new HashSet<SceneNodeId> { id };
        Collect(id);
        return result;

        void Collect(SceneNodeId parent)
        {
            foreach (var child in ChildrenOf(nodes, parent))
            {
                if (visited.Add(child.Id))
                {
                    result.Add(child.Id);
                    Collect(child.Id);
                }
            }
        }
    }

    /// <summary>True when re-parenting <paramref name="child"/> onto <paramref name="parent"/> would form a
    /// cycle: the parent is the child itself, or the parent already sits inside the child's subtree. The
    /// guard that keeps <see cref="EditorDocument.SetNodeParent"/> from corrupting the tree.</summary>
    public static bool WouldCreateCycle(IReadOnlyList<SceneNode> nodes, SceneNodeId child, SceneNodeId parent)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        if (child == parent)
        {
            return true;
        }

        foreach (var descendant in DescendantsOf(nodes, child))
        {
            if (descendant == parent)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The node's depth in the tree: 0 for a root, 1 for a child of a root, and so on. Cycle-safe
    /// and dangling-parent tolerant (the chain stops at the first missing or revisited ancestor). Returns 0
    /// for an id not present.</summary>
    public static int Depth(IReadOnlyList<SceneNode> nodes, SceneNodeId id)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        int depth = 0;
        var visited = new HashSet<SceneNodeId>();
        var current = Find(nodes, id);
        while (current is not null && visited.Add(current.Id) && current.ParentId is { } parent)
        {
            var ancestor = Find(nodes, parent);
            if (ancestor is null)
            {
                break;
            }

            depth++;
            current = ancestor;
        }

        return depth;
    }

    private static bool Exists(IReadOnlyList<SceneNode> nodes, SceneNodeId id) => Find(nodes, id) is not null;

    private static SceneNode? Find(IReadOnlyList<SceneNode> nodes, SceneNodeId id)
    {
        foreach (var node in nodes)
        {
            if (node.Id == id)
            {
                return node;
            }
        }

        return null;
    }
}
