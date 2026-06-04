using System.Collections.Generic;
using Opus.Engine.Renderer.Direct3D12.Assets;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>One instanced draw group: every <see cref="SceneNodeDraw"/> that shares
/// <see cref="MeshIndex"/> is uploaded as a contiguous slice of the per-frame instance buffer
/// at <see cref="InstanceOffset"/>, and the forward pass fans one <c>DrawIndexedInstanced</c>
/// per mesh primitive across all <see cref="InstanceCount"/> of them.</summary>
public readonly record struct SceneMeshBatch(int MeshIndex, int InstanceOffset, int InstanceCount);

/// <summary>Result of <see cref="SceneInstanceBatch.Build"/>: the flat per-instance buffer
/// (grouped contiguously by mesh, ready to upload as-is) and the per-mesh batch table that
/// slices it. Arrays (not lists) so the instance data marshals to the GPU without a copy,
/// matching <c>GpuScene</c>'s array-typed primitive/slice tables.</summary>
public readonly record struct SceneInstanceBatchResult(
    GpuInstanceData[] Instances,
    SceneMeshBatch[] Batches);

/// <summary>
/// Groups a flat <see cref="SceneNodeDraw"/> list into GPU instance batches: one batch per
/// distinct mesh, with that mesh's draws flattened into a contiguous slice of per-instance
/// world + tint records. Turns N draws of the same mesh into a single instanced draw per
/// primitive, which is the large-map draw-call win.
/// <para>
/// Pure and order-stable: meshes appear in first-seen order, instances within a mesh keep
/// their draw-list order. Opaque depth-tested rendering is order-independent, so regrouping by
/// mesh changes nothing visually. Per-instance tint lives in the buffer, so draws of the same
/// mesh batch together regardless of their individual tints.
/// </para>
/// </summary>
public static class SceneInstanceBatch
{
    public static SceneInstanceBatchResult Build(IReadOnlyList<SceneNodeDraw> draws)
    {
        ArgumentNullException.ThrowIfNull(draws);

        var order = new List<int>();
        var groups = new Dictionary<int, List<GpuInstanceData>>();
        for (var i = 0; i < draws.Count; i++)
        {
            var draw = draws[i];
            if (!groups.TryGetValue(draw.MeshIndex, out var list))
            {
                list = new List<GpuInstanceData>();
                groups.Add(draw.MeshIndex, list);
                order.Add(draw.MeshIndex);
            }

            list.Add(new GpuInstanceData
            {
                World = draw.World,
                BaseColorFactor = draw.TintFactor,
                UvOffset = draw.UvOffset,
            });
        }

        var instances = new List<GpuInstanceData>(draws.Count);
        var batches = new SceneMeshBatch[order.Count];
        for (var i = 0; i < order.Count; i++)
        {
            var list = groups[order[i]];
            batches[i] = new SceneMeshBatch(order[i], instances.Count, list.Count);
            instances.AddRange(list);
        }

        return new SceneInstanceBatchResult(instances.ToArray(), batches);
    }
}
