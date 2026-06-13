using System;
using System.Collections.Generic;
using System.Numerics;

namespace Opus.Editor.Core;

/// <summary>
/// Composes a node's world transform from its local transform and its ancestors'. A node's
/// <see cref="EditorTransform"/> is local to its parent, so the world matrix is the chain product
/// local · parent · grandparent · … in the engine's row-vector convention (a point row-vector is
/// transformed left to right). Cycle-safe and dangling-parent tolerant: the walk stops at the first
/// missing or already-visited ancestor, so a malformed scene file never loops and a node with no resolvable
/// parent behaves as a root. Pure — the viewport draw list and the pick list resolve world bounds through
/// this one place so what is drawn is exactly what is clicked.
/// </summary>
public static class SceneNodeTransforms
{
    public static Matrix4x4 WorldMatrix(EditorScene scene, SceneNodeId id)
    {
        ArgumentNullException.ThrowIfNull(scene);
        return WorldMatrix(scene.Nodes, id);
    }

    public static Matrix4x4 WorldMatrix(IReadOnlyList<SceneNode> nodes, SceneNodeId id)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var node = Find(nodes, id);
        if (node is null)
        {
            return Matrix4x4.Identity;
        }

        var matrix = node.Transform.ToMatrix();
        var visited = new HashSet<SceneNodeId> { id };
        var parentId = node.ParentId;
        while (parentId is { } parent && visited.Add(parent))
        {
            var ancestor = Find(nodes, parent);
            if (ancestor is null)
            {
                break;
            }

            matrix *= ancestor.Transform.ToMatrix();
            parentId = ancestor.ParentId;
        }

        return matrix;
    }

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
