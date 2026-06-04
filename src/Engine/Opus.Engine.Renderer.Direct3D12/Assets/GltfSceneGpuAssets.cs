using System.Collections.Generic;
using Opus.Content.Meshes;
using Opus.Foundation.Geometry;

namespace Opus.Engine.Renderer.Direct3D12.Assets;

/// <summary>Everything a renderer needs to draw a glTF asset: the original GLB bytes (so
/// downstream readers — material bindings, image atlas — can still walk the file), the
/// parsed scene tree, the flattened per-node draw list, the GPU-uploaded primitives, and
/// a scene-wide AABB for camera framing.
/// <para>
/// Disposal is the caller's job: walk <see cref="GpuScene"/>.<see cref="GpuScene.Primitives"/>
/// and dispose each primitive's <c>Vb</c> + <c>Ib</c>. <see cref="GltfSceneGpuAssets"/>
/// holds no native handles directly — only references.
/// </para>
/// </summary>
public sealed record GltfSceneGpuAssets(
    byte[] GlbBytes,
    GltfScene Scene,
    IReadOnlyList<SceneNodeDraw> NodeDraws,
    GpuScene GpuScene,
    Aabb Bounds)
{
    /// <summary>Per-mesh local-space AABB, indexed by glTF mesh index to match
    /// <see cref="GpuScene"/>.<see cref="Assets.GpuScene.SlicesByMesh"/>. Used by frustum
    /// culling to bound each node from its mesh and world transform. Empty when not computed
    /// (older callers that constructed the record without bounds still work).</summary>
    public IReadOnlyList<Aabb> MeshLocalBounds { get; init; } = System.Array.Empty<Aabb>();
}
