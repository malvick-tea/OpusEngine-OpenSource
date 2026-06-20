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
    public void Render(
        D3D12Renderer renderer,
        IReadOnlyList<ForwardSceneRenderLayer> layers,
        FrameCameraSet cameras,
        LightingSetup lighting,
        PostFxSetup postFx,
        SceneRenderTarget target)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(layers);
        ArgumentNullException.ThrowIfNull(cameras);
        ArgumentNullException.ThrowIfNull(lighting);
        ArgumentNullException.ThrowIfNull(postFx);
        if (layers.Count == 0)
        {
            throw new ArgumentException("At least one forward render layer is required.", nameof(layers));
        }

        var prepared = PrepareLayers(renderer.SwapChain.CurrentBackBufferIndex, layers, cameras);
        UploadSceneConstants(renderer.SwapChain.CurrentBackBufferIndex, cameras, lighting);

        var context = renderer.BeginFrame(cameras, lighting, postFx);
        try
        {
            var passes = BuildLayeredPasses(
                renderer.FrameGraphConcrete,
                target,
                prepared,
                lighting,
                renderer.SwapChain.CurrentBackBufferIndex);
            renderer.EndFrameWithoutPresent(context);
            RecordLayeredDrawCounts(passes);
        }
        catch
        {
            renderer.EndFrameWithoutPresent(context);
            throw;
        }
    }

    private IReadOnlyList<PreparedLayer> PrepareLayers(
        uint slot,
        IReadOnlyList<ForwardSceneRenderLayer> layers,
        FrameCameraSet cameras)
    {
        var plans = new PreparedLayerPlan[layers.Count];
        var instances = new List<GpuInstanceData>();
        var culled = 0;
        var demoted = 0;

        for (var index = 0; index < layers.Count; index++)
        {
            var layer = layers[index] ?? throw new ArgumentException(
                $"Forward render layer {index} is null.",
                nameof(layers));
            ArgumentNullException.ThrowIfNull(layer.GpuScene);
            ArgumentNullException.ThrowIfNull(layer.NodeDraws);
            ArgumentNullException.ThrowIfNull(layer.Materials);

            var draws = ResolveDraws(
                layer.NodeDraws,
                layer.MeshLocalBounds,
                layer.MeshLods,
                cameras);
            culled += LastCulledNodeCount;
            demoted += LastLodDemotedNodeCount;

            var batch = SceneInstanceBatch.Build(draws);
            var instanceBase = instances.Count;
            instances.AddRange(batch.Instances);
            var batches = new SceneMeshBatch[batch.Batches.Length];
            for (var batchIndex = 0; batchIndex < batches.Length; batchIndex++)
            {
                var source = batch.Batches[batchIndex];
                batches[batchIndex] = source with
                {
                    InstanceOffset = checked(source.InstanceOffset + instanceBase),
                };
            }

            plans[index] = new PreparedLayerPlan(
                layer.GpuScene,
                batches,
                layer.Materials);
        }

        var instanceBuffer = _instanceRing.Upload(slot, instances.ToArray());
        var prepared = new PreparedLayer[plans.Length];
        for (var index = 0; index < prepared.Length; index++)
        {
            var plan = plans[index];
            prepared[index] = new PreparedLayer(
                plan.GpuScene,
                plan.Batches,
                plan.Materials,
                instanceBuffer);
        }

        LastCulledNodeCount = culled;
        LastLodDemotedNodeCount = demoted;
        return prepared;
    }

    private IReadOnlyList<ForwardScenePass> BuildLayeredPasses(
        D3D12FrameGraph frameGraph,
        SceneRenderTarget target,
        IReadOnlyList<PreparedLayer> layers,
        LightingSetup lighting,
        uint sceneCbSlot)
    {
        var colorHandle = frameGraph.ImportTexture(target.Texture, initialState: target.InitialState);
        var hdrHandle = frameGraph.ImportTexture(
            _targets.HdrColor,
            initialState: ResourceStates.PixelShaderResource);
        var depthHandle = frameGraph.ImportTexture(
            _targets.Depth,
            initialState: ResourceStates.DepthWrite);
        var passes = new ForwardScenePass[layers.Count];

        for (var index = 0; index < layers.Count; index++)
        {
            var layer = layers[index];
            var pass = new ForwardScenePass(
                hdrHandle,
                depthHandle,
                _sceneRootSig,
                _scenePso,
                _sceneCbs[sceneCbSlot],
                layer.InstanceBuffer,
                _targets.HdrRtvHandle,
                _targets.DsvHandle,
                layer.GpuScene,
                layer.Batches,
                layer.Materials,
                lighting.Sky.HorizonColour,
                _width,
                _height,
                target.ClearAlpha,
                clearTargets: index == 0);
            passes[index] = pass;
            frameGraph.AddPass(pass);
        }

        frameGraph.AddPass(new TonemapPass(
            colorHandle,
            hdrHandle,
            _tonemapRootSig,
            _tonemapPso,
            target.Rtv,
            _targets.HdrSrvHeap,
            _targets.HdrSrvTable,
            target.Width,
            target.Height));
        frameGraph.EnsureFinalState(colorHandle, target.FinalState);
        frameGraph.EnsureFinalState(hdrHandle, ResourceStates.PixelShaderResource);
        frameGraph.EnsureFinalState(depthHandle, ResourceStates.DepthWrite);
        return passes;
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

    private sealed record PreparedLayer(
        GpuScene GpuScene,
        IReadOnlyList<SceneMeshBatch> Batches,
        IMaterialAtlas Materials,
        D3D12Buffer InstanceBuffer);

    private sealed record PreparedLayerPlan(
        GpuScene GpuScene,
        IReadOnlyList<SceneMeshBatch> Batches,
        IMaterialAtlas Materials);
}
