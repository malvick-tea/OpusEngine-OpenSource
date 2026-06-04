using System.Collections.Generic;
using Opus.Engine.Renderer.Direct3D12.Assets;
using Opus.Foundation.Geometry;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>
/// CPU frustum culling for a flat <see cref="SceneNodeDraw"/> list: drops nodes whose
/// world-space bounds fall fully outside the view frustum before they reach the draw loop.
/// Pure and allocation-light — when nothing can be culled it returns the input list unchanged.
/// Conservative by construction (it reuses <see cref="Frustum.Intersects"/>), so it never
/// removes a node that is visible or straddling the frustum edge.
/// </summary>
public static class SceneNodeCuller
{
    /// <summary>Returns the visible subset of <paramref name="draws"/> and how many nodes were
    /// culled. A node is kept when <paramref name="meshLocalBounds"/> is null, has no entry for
    /// the node's mesh, or that entry is empty — culling never drops a node it cannot bound.</summary>
    public static SceneCullResult Cull(
        IReadOnlyList<SceneNodeDraw> draws,
        IReadOnlyList<Aabb>? meshLocalBounds,
        in Frustum frustum)
    {
        ArgumentNullException.ThrowIfNull(draws);
        if (meshLocalBounds is null)
        {
            return new SceneCullResult(draws, 0);
        }

        var visible = new List<SceneNodeDraw>(draws.Count);
        var culled = 0;
        for (var i = 0; i < draws.Count; i++)
        {
            var draw = draws[i];
            if (IsVisible(draw, meshLocalBounds, in frustum))
            {
                visible.Add(draw);
            }
            else
            {
                culled++;
            }
        }

        return new SceneCullResult(visible, culled);
    }

    private static bool IsVisible(
        SceneNodeDraw draw, IReadOnlyList<Aabb> meshLocalBounds, in Frustum frustum)
    {
        if (draw.MeshIndex < 0 || draw.MeshIndex >= meshLocalBounds.Count)
        {
            return true;
        }

        var local = meshLocalBounds[draw.MeshIndex];
        if (local.IsEmpty)
        {
            return true;
        }

        return frustum.Intersects(local.Transform(draw.World));
    }
}

/// <summary>Outcome of a cull pass: the visible draws (the original list when nothing was
/// culled) and the number of nodes removed.</summary>
public readonly record struct SceneCullResult(IReadOnlyList<SceneNodeDraw> Visible, int CulledCount);
