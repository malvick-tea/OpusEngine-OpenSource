using System.Collections.Generic;
using Opus.Engine.Renderer.Direct3D12.Assets;
using Opus.Foundation.Geometry;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>One material-isolated opaque layer submitted into a shared forward depth buffer.</summary>
public sealed record ForwardSceneRenderLayer(
    GpuScene GpuScene,
    IReadOnlyList<SceneNodeDraw> NodeDraws,
    IMaterialAtlas Materials,
    IReadOnlyList<Aabb>? MeshLocalBounds = null,
    IReadOnlyList<SceneMeshLod>? MeshLods = null);
