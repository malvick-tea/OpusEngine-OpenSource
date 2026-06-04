using System.Collections.Generic;
using Opus.Engine.FrameGraph;
using Opus.Engine.Renderer.Direct3D12.Assets;
using Opus.Foundation.Geometry;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

public sealed unsafe partial class D3D12ForwardSceneRenderer
{
    /// <summary>Applies opt-in culling and coarse LOD before instance batching.</summary>
    private IReadOnlyList<SceneNodeDraw> ResolveDraws(
        IReadOnlyList<SceneNodeDraw> nodeDraws,
        IReadOnlyList<Aabb>? meshLocalBounds,
        IReadOnlyList<SceneMeshLod>? meshLods,
        FrameCameraSet cameras)
    {
        var camera = cameras.Main;
        var draws = nodeDraws;

        if (meshLocalBounds is null || meshLocalBounds.Count == 0)
        {
            LastCulledNodeCount = 0;
        }
        else
        {
            var frustum = Frustum.FromViewProjection(camera.View * camera.Projection);
            var culled = SceneNodeCuller.Cull(nodeDraws, meshLocalBounds, in frustum);
            LastCulledNodeCount = culled.CulledCount;
            draws = culled.Visible;
        }

        if (meshLods is null || meshLods.Count == 0 || meshLocalBounds is null || meshLocalBounds.Count == 0)
        {
            LastLodDemotedNodeCount = 0;
            return draws;
        }

        var lod = SceneLodSelector.Select(draws, meshLods, meshLocalBounds, camera.PositionWorld);
        LastLodDemotedNodeCount = lod.DemotedNodeCount;
        return lod.Draws;
    }
}
