using System;
using System.Collections.Generic;
using System.Numerics;

namespace Opus.Content.Meshes;

/// <summary>
/// Helpers for flattening a scene-tree hierarchy into per-node world matrices.
/// Convention: <c>worldChild = localChild × worldParent</c> in
/// <see cref="System.Numerics"/> row-vector layout (matches the engine's HLSL
/// <c>mul(row, world)</c> calls).
/// </summary>
public static class SceneTreeMath
{
    /// <summary>
    /// Walks the node hierarchy from the roots, returns a world transform per node
    /// indexed the same way as <see cref="GltfScene.Nodes"/>. Throws
    /// <see cref="InvalidOperationException"/> if the hierarchy has a cycle.
    /// </summary>
    public static Matrix4x4[] ComputeWorldTransforms(GltfScene scene)
        => ComputeWorldTransforms(scene, localTransforms: null);

    /// <summary>
    /// Walks the node hierarchy using <paramref name="localTransforms"/> in place of the
    /// authored local matrices. This lets a renderer pose named nodes at runtime while
    /// retaining the imported hierarchy.
    /// </summary>
    public static Matrix4x4[] ComputeWorldTransforms(
        GltfScene scene,
        IReadOnlyList<Matrix4x4>? localTransforms)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var nodes = scene.Nodes;
        if (localTransforms is not null && localTransforms.Count != nodes.Length)
        {
            throw new ArgumentException(
                $"localTransforms.Count ({localTransforms.Count}) must match scene.Nodes.Length ({nodes.Length}).",
                nameof(localTransforms));
        }

        var worlds = new Matrix4x4[nodes.Length];
        var visited = new bool[nodes.Length];

        var stack = new System.Collections.Generic.Stack<int>(nodes.Length);
        foreach (var rootIdx in scene.RootNodes)
        {
            stack.Push(rootIdx);
        }

        while (stack.Count > 0)
        {
            var nodeIdx = stack.Pop();
            if (visited[nodeIdx])
            {
                throw new InvalidOperationException($"Cycle detected at node {nodeIdx}.");
            }

            visited[nodeIdx] = true;

            var node = nodes[nodeIdx];
            var parentWorld = node.ParentIndex >= 0 ? worlds[node.ParentIndex] : Matrix4x4.Identity;
            var local = localTransforms is null ? node.LocalTransform : localTransforms[nodeIdx];
            worlds[nodeIdx] = local * parentWorld;

            foreach (var child in node.ChildIndices)
            {
                stack.Push(child);
            }
        }

        return worlds;
    }
}
