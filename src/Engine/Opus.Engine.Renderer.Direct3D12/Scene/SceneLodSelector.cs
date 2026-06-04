using System;
using System.Collections.Generic;
using System.Numerics;
using Opus.Engine.Renderer.Direct3D12.Assets;
using Opus.Foundation.Geometry;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>
/// Coarse distance LOD for a flat <see cref="SceneNodeDraw"/> list: rewrites each draw's mesh to
/// the level appropriate for its world-space distance to the camera, so a distant instance is
/// reselected to a cheaper (lower-triangle) mesh variant before it reaches the instance batcher.
/// Pure and allocation-light — when no LOD data is supplied it returns the input list unchanged.
/// Conservative by construction (it reuses the same per-mesh bounds frustum culling consumes): a
/// draw it cannot place keeps its incoming mesh, so LOD never coarsens a node it cannot measure.
/// Runs between <see cref="SceneNodeCuller"/> and <see cref="SceneInstanceBatch"/>; see ADR-0032.
/// </summary>
public static class SceneLodSelector
{
    /// <summary>Returns the LOD-resolved draws and how many were reselected below their finest
    /// level. A draw keeps its incoming mesh when <paramref name="meshLods"/> or
    /// <paramref name="meshLocalBounds"/> is null, has no entry for the draw's mesh, the chain is
    /// empty, or the bounds entry is empty — LOD never changes a draw it cannot distance-measure.
    /// Distance is the nearest-point distance from <paramref name="cameraPositionWorld"/> to the
    /// node's world AABB (<c>meshLocalBounds[mesh].Transform(world)</c>), so a large object stays
    /// fine longer and an object the camera sits inside resolves to the finest level.</summary>
    public static SceneLodResult Select(
        IReadOnlyList<SceneNodeDraw> draws,
        IReadOnlyList<SceneMeshLod>? meshLods,
        IReadOnlyList<Aabb>? meshLocalBounds,
        Vector3 cameraPositionWorld)
    {
        ArgumentNullException.ThrowIfNull(draws);
        if (meshLods is null || meshLocalBounds is null)
        {
            return new SceneLodResult(draws, 0);
        }

        var resolved = new List<SceneNodeDraw>(draws.Count);
        var demoted = 0;
        for (var i = 0; i < draws.Count; i++)
        {
            var draw = draws[i];
            var meshIndex = SelectMeshIndex(draw, meshLods, meshLocalBounds, cameraPositionWorld);
            if (meshIndex != draw.MeshIndex)
            {
                draw = draw with { MeshIndex = meshIndex };
                demoted++;
            }

            resolved.Add(draw);
        }

        return new SceneLodResult(resolved, demoted);
    }

    private static int SelectMeshIndex(
        SceneNodeDraw draw,
        IReadOnlyList<SceneMeshLod> meshLods,
        IReadOnlyList<Aabb> meshLocalBounds,
        Vector3 cameraPositionWorld)
    {
        if (draw.MeshIndex < 0 || draw.MeshIndex >= meshLods.Count)
        {
            return draw.MeshIndex;
        }

        var lod = meshLods[draw.MeshIndex];
        if (!lod.HasLevels || draw.MeshIndex >= meshLocalBounds.Count)
        {
            return draw.MeshIndex;
        }

        var local = meshLocalBounds[draw.MeshIndex];
        if (local.IsEmpty)
        {
            return draw.MeshIndex;
        }

        var worldBounds = local.Transform(draw.World);
        var distanceSquared = NearestPointDistanceSquared(cameraPositionWorld, worldBounds);
        return SelectLevelMeshIndex(lod.Levels, distanceSquared);
    }

    /// <summary>Picks the first level whose squared threshold covers the squared distance,
    /// falling back to the coarsest (last) level. Compares in squared space to avoid the square
    /// root; a <see cref="float.PositiveInfinity"/> threshold squares to infinity and so always
    /// covers, which is why the coarsest level uses it.</summary>
    private static int SelectLevelMeshIndex(IReadOnlyList<SceneMeshLodLevel> levels, float distanceSquared)
    {
        for (var i = 0; i < levels.Count; i++)
        {
            var maxDistance = levels[i].MaxCameraDistance;
            if (distanceSquared <= maxDistance * maxDistance)
            {
                return levels[i].MeshIndex;
            }
        }

        return levels[levels.Count - 1].MeshIndex;
    }

    /// <summary>Squared distance from <paramref name="point"/> to the nearest point on
    /// <paramref name="box"/> — zero when the point is inside the box. Per-axis clamp of the
    /// centre offset by the extents, the standard point-to-AABB distance.</summary>
    private static float NearestPointDistanceSquared(Vector3 point, Aabb box)
    {
        var clamped = Vector3.Max(Vector3.Abs(point - box.Centre) - box.Extents, Vector3.Zero);
        return clamped.LengthSquared();
    }
}

/// <summary>Outcome of a LOD pass: the resolved draws (the original list when no LOD applied) and
/// the number reselected below their finest level — the LOD analogue of
/// <see cref="SceneCullResult.CulledCount"/>.</summary>
public readonly record struct SceneLodResult(IReadOnlyList<SceneNodeDraw> Draws, int DemotedNodeCount);
