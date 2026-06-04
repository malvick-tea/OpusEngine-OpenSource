using System.Collections.Generic;
using Opus.Engine.Renderer.Direct3D12.Assets;
using Opus.Foundation.Geometry;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>One opaque layer submitted to <see cref="D3D12ForwardSceneRenderer"/>.
/// Layers share the same HDR and depth targets, but each may use its own scene and atlas.</summary>
public sealed record ForwardSceneRenderLayer(
    GpuScene GpuScene,
    IReadOnlyList<SceneNodeDraw> NodeDraws,
    IMaterialAtlas Materials,
    IReadOnlyList<Aabb>? MeshLocalBounds = null,
    IReadOnlyList<SceneMeshLod>? MeshLods = null);
