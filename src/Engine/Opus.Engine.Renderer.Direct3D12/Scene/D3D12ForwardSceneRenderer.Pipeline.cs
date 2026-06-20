using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Opus.Engine.FrameGraph;
using Opus.Engine.Renderer.Direct3D12.Assets;
using Opus.Engine.Rhi;
using Opus.Engine.Rhi.Direct3D12;
using Silk.NET.Direct3D12;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

public sealed unsafe partial class D3D12ForwardSceneRenderer
{
    private static void WriteSceneConstants(
        D3D12Buffer buffer,
        in ForwardSceneConstants value)
    {
        var bytes = new byte[SceneCbSize];
        MemoryMarshal.Write(
            bytes.AsSpan(0, Marshal.SizeOf<ForwardSceneConstants>()),
            in value);
        buffer.Upload(bytes);
    }

    private void UploadSceneConstants(
        uint slot,
        FrameCameraSet cameras,
        LightingSetup lighting)
    {
        var constants = ForwardSceneConstants.From(
            cameras.Main,
            lighting.Sun,
            lighting.Sky.ZenithColour);
        WriteSceneConstants(_sceneCbs[slot], in constants);
    }

    private ForwardScenePass BuildPasses(
        D3D12FrameGraph frameGraph,
        FrameGraphResource colorHandle,
        CpuDescriptorHandle colorRtv,
        int viewportWidth,
        int viewportHeight,
        ResourceStates colorFinalState,
        GpuScene gpuScene,
        IReadOnlyList<SceneMeshBatch> batches,
        D3D12Buffer instanceBuffer,
        IMaterialAtlas materials,
        LightingSetup lighting,
        uint sceneCbSlot,
        float clearAlpha)
    {
        var hdrHandle = frameGraph.ImportTexture(
            _targets.HdrColor,
            initialState: ResourceStates.PixelShaderResource);
        var depthHandle = frameGraph.ImportTexture(
            _targets.Depth,
            initialState: ResourceStates.DepthWrite);
        var scenePass = new ForwardScenePass(
            hdrHandle,
            depthHandle,
            _sceneRootSig,
            _scenePso,
            _sceneCbs[sceneCbSlot],
            instanceBuffer,
            _targets.HdrRtvHandle,
            _targets.DsvHandle,
            gpuScene,
            batches,
            materials,
            lighting.Sky.HorizonColour,
            _width,
            _height,
            clearAlpha);
        frameGraph.AddPass(scenePass);
        frameGraph.AddPass(new TonemapPass(
            colorHandle,
            hdrHandle,
            _tonemapRootSig,
            _tonemapPso,
            colorRtv,
            _targets.HdrSrvHeap,
            _targets.HdrSrvTable,
            viewportWidth,
            viewportHeight));
        frameGraph.EnsureFinalState(colorHandle, colorFinalState);
        frameGraph.EnsureFinalState(
            hdrHandle,
            ResourceStates.PixelShaderResource);
        frameGraph.EnsureFinalState(depthHandle, ResourceStates.DepthWrite);
        return scenePass;
    }

    private static void ValidateRenderArguments(
        D3D12Renderer renderer,
        IReadOnlyList<SceneNodeDraw> nodeDraws,
        IMaterialAtlas materials,
        FrameCameraSet cameras,
        LightingSetup lighting,
        PostFxSetup postFx)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(nodeDraws);
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentNullException.ThrowIfNull(cameras);
        ArgumentNullException.ThrowIfNull(lighting);
        ArgumentNullException.ThrowIfNull(postFx);
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);
}
