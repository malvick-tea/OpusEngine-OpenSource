using System;
using System.Collections.Generic;
using Opus.Engine.FrameGraph;
using Opus.Engine.Renderer.Direct3D12.Assets;
using Opus.Engine.Rhi;
using Opus.Engine.Rhi.Direct3D12;
using Silk.NET.Direct3D12;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

public sealed unsafe partial class D3D12ForwardSceneRenderer
{
    private readonly List<SceneInstanceBufferRing> _layerInstanceRings = new();

    /// <summary>Renders several opaque scene layers into one offscreen target. The first layer
    /// clears HDR/depth; later layers preserve both so objects across layers occlude correctly.</summary>
    public void Render(
        D3D12Renderer renderer,
        IReadOnlyList<ForwardSceneRenderLayer> layers,
        FrameCameraSet cameras,
        LightingSetup lighting,
        PostFxSetup postFx,
        SceneRenderTarget target)
    {
        ThrowIfDisposed();
        ValidateLayeredRenderArguments(renderer, layers, cameras, lighting, postFx, target);

        var slot = renderer.SwapChain.CurrentBackBufferIndex;
        UploadSceneConstants(slot, cameras, lighting);
        var prepared = PrepareLayers(slot, layers, cameras);

        var ctx = renderer.BeginFrame(cameras, lighting, postFx);
        try
        {
            var passes = AddLayeredPasses(renderer.FrameGraphConcrete, target, prepared, lighting, slot);
            renderer.EndFrameWithoutPresent(ctx);
            RecordLayeredDrawCounts(passes);
        }
        catch
        {
            renderer.EndFrameWithoutPresent(ctx);
            throw;
        }
    }

    private IReadOnlyList<PreparedLayer> PrepareLayers(
        uint slot,
        IReadOnlyList<ForwardSceneRenderLayer> layers,
        FrameCameraSet cameras)
    {
        var prepared = new List<PreparedLayer>(layers.Count);
        var culled = 0;
        var lodDemoted = 0;
        for (var i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            var draws = ResolveDraws(layer.NodeDraws, layer.MeshLocalBounds, layer.MeshLods, cameras);
            culled += LastCulledNodeCount;
            lodDemoted += LastLodDemotedNodeCount;

            var batch = SceneInstanceBatch.Build(draws);
            var instanceBuffer = LayerInstanceRing(i).Upload(slot, batch.Instances);
            prepared.Add(new PreparedLayer(layer.GpuScene, batch.Batches, instanceBuffer, layer.Materials));
        }

        LastCulledNodeCount = culled;
        LastLodDemotedNodeCount = lodDemoted;
        return prepared;
    }

    private IReadOnlyList<ForwardScenePass> AddLayeredPasses(
        D3D12FrameGraph fg,
        SceneRenderTarget target,
        IReadOnlyList<PreparedLayer> layers,
        LightingSetup lighting,
        uint sceneCbSlot)
    {
        var colorHandle = fg.ImportTexture(target.Texture, initialState: target.InitialState);
        var hdrHandle = fg.ImportTexture(_targets.HdrColor, initialState: ResourceStates.PixelShaderResource);
        var depthHandle = fg.ImportTexture(_targets.Depth, initialState: ResourceStates.DepthWrite);
        var passes = new List<ForwardScenePass>(layers.Count);

        for (var i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            var pass = new ForwardScenePass(
                hdrHandle, depthHandle, _sceneRootSig, _scenePso,
                _sceneCbs[sceneCbSlot], layer.InstanceBuffer, _targets.HdrRtvHandle, _targets.DsvHandle,
                layer.GpuScene, layer.Batches, layer.Materials, lighting.Sky.HorizonColour, _width, _height,
                target.ClearAlpha, clearTargets: i == 0);
            fg.AddPass(pass);
            passes.Add(pass);
        }

        fg.AddPass(new TonemapPass(
            colorHandle, hdrHandle, _tonemapRootSig, _tonemapPso,
            target.Rtv, _targets.HdrSrvHeap, _targets.HdrSrvTable,
            target.Width, target.Height));

        fg.EnsureFinalState(colorHandle, target.FinalState);
        fg.EnsureFinalState(hdrHandle, ResourceStates.PixelShaderResource);
        fg.EnsureFinalState(depthHandle, ResourceStates.DepthWrite);
        return passes;
    }

    private SceneInstanceBufferRing LayerInstanceRing(int layerIndex)
    {
        while (_layerInstanceRings.Count <= layerIndex)
        {
            _layerInstanceRings.Add(new SceneInstanceBufferRing(
                _device, D3D12SwapChain.BufferCount, $"{_namePrefix}.layer.{_layerInstanceRings.Count}"));
        }

        return _layerInstanceRings[layerIndex];
    }

    private void RecordLayeredDrawCounts(IReadOnlyList<ForwardScenePass> passes)
    {
        LastDrawnPrimitiveCount = 0;
        LastDrawCallCount = 0;
        LastDrawnIndexCount = 0;
        foreach (var pass in passes)
        {
            LastDrawnPrimitiveCount += pass.DrawnPrimitiveInstanceCount;
            LastDrawCallCount += pass.DrawCallCount;
            LastDrawnIndexCount += pass.DrawnIndexCount;
        }
    }

    private void DisposeLayerInstanceRings()
    {
        foreach (var ring in _layerInstanceRings)
        {
            ring.Dispose();
        }
    }

    private static void ValidateLayeredRenderArguments(
        D3D12Renderer renderer,
        IReadOnlyList<ForwardSceneRenderLayer> layers,
        FrameCameraSet cameras,
        LightingSetup lighting,
        PostFxSetup postFx,
        SceneRenderTarget target)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(layers);
        if (layers.Count == 0)
        {
            throw new ArgumentException("At least one render layer is required.", nameof(layers));
        }

        foreach (var layer in layers)
        {
            ArgumentNullException.ThrowIfNull(layer.GpuScene);
            ArgumentNullException.ThrowIfNull(layer.NodeDraws);
            ArgumentNullException.ThrowIfNull(layer.Materials);
        }

        ArgumentNullException.ThrowIfNull(cameras);
        ArgumentNullException.ThrowIfNull(lighting);
        ArgumentNullException.ThrowIfNull(postFx);
    }

    private readonly record struct PreparedLayer(
        GpuScene GpuScene,
        IReadOnlyList<SceneMeshBatch> Batches,
        D3D12Buffer InstanceBuffer,
        IMaterialAtlas Materials);
}
