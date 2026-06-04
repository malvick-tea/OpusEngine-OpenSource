using System;
using System.Collections.Generic;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>One level in a mesh's coarse-LOD chain: the <see cref="GpuScene"/> mesh index to
/// draw at this level, and the maximum camera distance (world units) at which the level still
/// applies. Levels are ordered finest-first with ascending <see cref="MaxCameraDistance"/>; the
/// coarsest level typically uses <see cref="float.PositiveInfinity"/> so it covers everything
/// beyond the previous threshold. A level's <see cref="MeshIndex"/> points at a (lower-triangle)
/// mesh already spliced into the same <see cref="GpuScene"/>, per ADR-0028.</summary>
public readonly record struct SceneMeshLodLevel(int MeshIndex, float MaxCameraDistance);

/// <summary>A logical mesh's coarse-LOD chain: the ordered levels used to reselect a distant
/// instance to a cheaper mesh variant. Built one entry per mesh in a list parallel to
/// <see cref="GpuScene"/>.<see cref="Assets.GpuScene.SlicesByMesh"/> (and to the
/// <c>meshLocalBounds</c> table frustum culling already consumes); a mesh with no chain uses
/// <see cref="None"/>. Consumed by <see cref="SceneLodSelector"/> between culling (M5.2) and
/// instance-batching (M5.3) — see ADR-0032.</summary>
/// <remarks>
/// The finest level (index 0) normally points back at the logical mesh itself, so a near
/// instance reselects to the same mesh and renders byte-identically to the no-LOD path. The
/// engine reselects among LOD meshes; it never authors them — the coarse variants come from the
/// content layer, consistent with Opus shipping the pipeline and not the assets.
/// </remarks>
public readonly record struct SceneMeshLod(IReadOnlyList<SceneMeshLodLevel> Levels)
{
    /// <summary>An empty chain — the mesh has no LOD and always renders at its authored detail.
    /// Use for every mesh in a scene's LOD table that does not define coarse variants.</summary>
    public static SceneMeshLod None => new(Array.Empty<SceneMeshLodLevel>());

    /// <summary>True when the chain defines at least one level. An empty chain is treated by
    /// <see cref="SceneLodSelector"/> as "no LOD" (the draw keeps its incoming mesh).</summary>
    public bool HasLevels => Levels is { Count: > 0 };

    /// <summary>Builds a validated chain: at least one level, every mesh index non-negative,
    /// and strictly ascending <see cref="SceneMeshLodLevel.MaxCameraDistance"/> (finest first).
    /// Throws <see cref="ArgumentException"/> when the chain is empty or unordered and
    /// <see cref="ArgumentOutOfRangeException"/> on a negative mesh index, so a malformed
    /// authored chain fails loudly at construction rather than mis-selecting at draw time.</summary>
    public static SceneMeshLod Create(params SceneMeshLodLevel[] levels)
    {
        ArgumentNullException.ThrowIfNull(levels);
        if (levels.Length == 0)
        {
            throw new ArgumentException("A LOD chain needs at least one level.", nameof(levels));
        }

        for (var i = 0; i < levels.Length; i++)
        {
            if (levels[i].MeshIndex < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(levels), levels[i].MeshIndex, "LOD level mesh index must be non-negative.");
            }

            if (i > 0 && levels[i].MaxCameraDistance <= levels[i - 1].MaxCameraDistance)
            {
                throw new ArgumentException(
                    "LOD levels must be ordered finest-first with strictly ascending MaxCameraDistance.",
                    nameof(levels));
            }
        }

        return new SceneMeshLod(levels);
    }
}
