using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Opus.Engine.FrameGraph;
using Opus.Engine.Renderer.Direct3D12.Assets;
using Opus.Engine.Rhi;
using Opus.Engine.Rhi.Direct3D12;
using Opus.Foundation.Geometry;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>High-level scene renderer for the Direct3D 12 backend. Composes the canonical
/// 2-pass forward set (<see cref="ForwardScenePass"/> writes HDR + depth; <see cref="TonemapPass"/>
/// ACES-tonemaps HDR to the swap-chain backbuffer) so callers can render a glTF scene
/// with a single call instead of authoring root signatures, PSOs, descriptor heaps, and
/// pass scaffolding by hand.
/// <para>
/// Owns its own shaders, root signatures, PSOs, the ring of scene constant buffers, and
/// the <see cref="ForwardSceneTargets"/> (HDR + depth). The <see cref="D3D12Renderer"/>
/// passed to <see cref="Render"/> still owns the frame graph + command list — the scene
/// renderer composes onto it via the typed <see cref="D3D12Renderer.FrameGraphConcrete"/>
/// surface. Drives the <c>BeginFrame → AddPass → EndFrame</c> bracket internally so
/// callers don't touch the renderer lifecycle directly.
/// </para>
/// <para>
/// Designed for the M3-wrap.b minimum-viable forward path: one directional sun + flat
/// ambient, single-texture albedo via <see cref="IMaterialAtlas"/>, no tile-based light
/// culling yet (that's the Forward+ extension for M3-wrap.b.1). Local lights on
/// <see cref="LightingSetup.LocalLights"/> are ignored at this milestone.
/// </para></summary>
public sealed unsafe partial class D3D12ForwardSceneRenderer : IDisposable
{
    private const int SceneCbSize = 256;

    private readonly D3D12RhiDevice _device;
    private readonly string _namePrefix;
    private readonly D3D12RootSignature _sceneRootSig;
    private readonly D3D12GraphicsPipeline _scenePso;
    private readonly D3D12RootSignature _tonemapRootSig;
    private readonly D3D12GraphicsPipeline _tonemapPso;
    private readonly D3D12Buffer[] _sceneCbs;
    private readonly SceneInstanceBufferRing _instanceRing;
    private ForwardSceneTargets _targets;
    private int _width;
    private int _height;
    private bool _disposed;

    /// <summary>Total primitive-instances the most recent <see cref="Render"/> call submitted
    /// (instances × mesh primitive count, summed over batches). Exposed for test assertions; the
    /// value reflects the last pass's <see cref="ForwardScenePass.DrawnPrimitiveInstanceCount"/>.
    /// Equals the pre-instancing per-draw primitive total, so frustum culling still lowers it.</summary>
    public int LastDrawnPrimitiveCount { get; private set; }

    /// <summary>Number of indexed draw calls the most recent <see cref="Render"/> call issued —
    /// one per mesh primitive per batch, independent of instance count. The GPU-instancing win
    /// metric: with N repeated copies of a mesh this stays at the mesh's primitive count instead
    /// of N times it, while <see cref="LastDrawnPrimitiveCount"/> still counts all N.</summary>
    public int LastDrawCallCount { get; private set; }

    /// <summary>Number of scene nodes the most recent <see cref="Render"/> call dropped by
    /// frustum culling before the draw loop. Zero when the caller passed no per-mesh bounds
    /// (culling is opt-in) or when every node was inside the frustum. Exposed for test and
    /// telemetry assertions alongside <see cref="LastDrawnPrimitiveCount"/>.</summary>
    public int LastCulledNodeCount { get; private set; }

    /// <summary>Total indices the most recent <see cref="Render"/> call submitted (instances ×
    /// mesh index count, summed over batches) — the triangle-budget metric coarse LOD reduces.
    /// Reselecting distant instances to lower-index mesh variants lowers this while
    /// <see cref="LastDrawCallCount"/> and <see cref="LastDrawnPrimitiveCount"/> stay put.</summary>
    public int LastDrawnIndexCount { get; private set; }

    /// <summary>Number of instances the most recent <see cref="Render"/> call reselected below
    /// their finest LOD level (drawn at a coarser mesh variant). Zero when the caller passed no
    /// LOD chains (coarse LOD is opt-in), no per-mesh bounds to measure distance, or every
    /// instance was near enough for its finest level. The LOD analogue of
    /// <see cref="LastCulledNodeCount"/>.</summary>
    public int LastLodDemotedNodeCount { get; private set; }

    public D3D12ForwardSceneRenderer(
        D3D12RhiDevice device,
        D3D12ShaderCompiler compiler,
        Format backbufferFormat,
        int viewportWidth,
        int viewportHeight,
        string namePrefix = "forward")
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(compiler);

        _device = device;
        _namePrefix = namePrefix;
        _width = viewportWidth;
        _height = viewportHeight;

        var resources = CreateResources(
            device,
            compiler,
            backbufferFormat,
            viewportWidth,
            viewportHeight,
            namePrefix);
        _sceneRootSig = resources.SceneRootSignature;
        _scenePso = resources.ScenePipeline;
        _tonemapRootSig = resources.TonemapRootSignature;
        _tonemapPso = resources.TonemapPipeline;
        _targets = resources.Targets;
        _sceneCbs = resources.SceneConstantBuffers;
        _instanceRing = resources.InstanceRing;
    }

    /// <summary>Recreates the size-dependent offscreen targets (HDR colour + depth + their
    /// descriptor heaps) at the new viewport size, preserving the compiled shaders, PSOs,
    /// root signatures, and scene constant buffers — so a window resize never recompiles a
    /// shader. The new targets are built before the old ones are released, so a failed
    /// allocation leaves the renderer on its previous targets. No-op on a degenerate or
    /// unchanged size. The caller must have drained the GPU first (the host runs this right
    /// after <see cref="D3D12SwapChain.Resize"/>, which waits for idle).</summary>
    public void Resize(int viewportWidth, int viewportHeight)
    {
        ThrowIfDisposed();
        if (viewportWidth <= 0 || viewportHeight <= 0 || (viewportWidth == _width && viewportHeight == _height))
        {
            return;
        }

        var newTargets = new ForwardSceneTargets(_device, viewportWidth, viewportHeight, _namePrefix);
        _targets.Dispose();
        _targets = newTargets;
        _width = viewportWidth;
        _height = viewportHeight;
    }

    /// <summary>Renders <paramref name="scene"/> through the canonical forward + tonemap
    /// pass set into <paramref name="renderer"/>'s current swap-chain backbuffer using the
    /// asset's own flattened node-graph as the draw list. Frustum culling runs against the
    /// asset's <see cref="GltfSceneGpuAssets.MeshLocalBounds"/> when present (empty = every
    /// node kept). Equivalent to the custom-draws overload with
    /// <c>nodeDraws = scene.NodeDraws</c> and <c>meshLocalBounds = scene.MeshLocalBounds</c>.</summary>
    public void Render(
        D3D12Renderer renderer,
        GltfSceneGpuAssets scene,
        IMaterialAtlas materials,
        FrameCameraSet cameras,
        LightingSetup lighting,
        PostFxSetup postFx)
    {
        ArgumentNullException.ThrowIfNull(scene);
        Render(renderer, scene.GpuScene, scene.NodeDraws, materials, cameras, lighting, postFx, scene.MeshLocalBounds);
    }

    /// <summary>Renders <paramref name="gpuScene"/> with a caller-supplied list of node
    /// draws — opens an instancing surface for game code that places multiple copies of an
    /// asset, applies per-frame transforms (e.g. a tank moving in the Sim), or composes
    /// instances from sources other than a single glTF node graph. The pass calls
    /// <see cref="D3D12Renderer.BeginFrame"/> / <see cref="D3D12Renderer.EndFrame"/>
    /// internally — callers must not bracket the call themselves.
    /// <para>
    /// Supplying <paramref name="meshLocalBounds"/> (indexed by mesh to match
    /// <see cref="GpuScene.SlicesByMesh"/>) enables CPU frustum culling: nodes whose
    /// world-space bounds fall fully outside the camera frustum are dropped before the draw
    /// loop and counted in <see cref="LastCulledNodeCount"/>. Pass null (the default) to
    /// render every node exactly as before — culling is opt-in and never changes pixels,
    /// only which off-screen nodes are skipped.</para>
    /// <para>
    /// Supplying <paramref name="meshLods"/> (also indexed by mesh, alongside
    /// <paramref name="meshLocalBounds"/>, which it reuses for the camera-distance bounds)
    /// enables coarse LOD: each surviving node is reselected to the cheaper mesh variant
    /// appropriate for its distance before instance-batching, counted in
    /// <see cref="LastLodDemotedNodeCount"/> (see ADR-0032). LOD needs the bounds for distance,
    /// so it only applies when both are supplied; null (the default) renders at full detail.</para></summary>
    public void Render(
        D3D12Renderer renderer,
        GpuScene gpuScene,
        IReadOnlyList<SceneNodeDraw> nodeDraws,
        IMaterialAtlas materials,
        FrameCameraSet cameras,
        LightingSetup lighting,
        PostFxSetup postFx,
        IReadOnlyList<Aabb>? meshLocalBounds = null,
        IReadOnlyList<SceneMeshLod>? meshLods = null)
    {
        ThrowIfDisposed();
        ValidateRenderArguments(renderer, nodeDraws, materials, cameras, lighting, postFx);
        var draws = ResolveDraws(nodeDraws, meshLocalBounds, meshLods, cameras);

        var swapChain = renderer.SwapChain;
        var slot = swapChain.CurrentBackBufferIndex;
        UploadSceneConstants(slot, cameras, lighting);
        var (batches, instanceBuffer) = PrepareInstances(slot, draws);

        var ctx = renderer.BeginFrame(cameras, lighting, postFx);
        try
        {
            var fg = renderer.FrameGraphConcrete;
            var backBufferHandle = fg.ImportNativeTexture(
                "swap.backbuffer", swapChain.CurrentBackBuffer, swapChain.Width, swapChain.Height,
                swapChain.Format, initialState: ResourceStates.Present);
            var scenePass = BuildPasses(
                fg, backBufferHandle, swapChain.CurrentRenderTargetView,
                swapChain.Width, swapChain.Height, ResourceStates.Present,
                gpuScene, batches, instanceBuffer, materials, lighting, slot, clearAlpha: 1f);
            renderer.EndFrame(ctx);
            RecordDrawCounts(scenePass);
        }
        catch
        {
            renderer.EndFrame(ctx);
            throw;
        }
    }

    /// <summary>Renders <paramref name="gpuScene"/> into <paramref name="target"/> instead
    /// of the renderer's swap chain — used when the scene composites into a 2D UI quad
    /// instead of being presented directly. Uses
    /// <see cref="D3D12Renderer.EndFrameWithoutPresent"/> so the host's UI pass owns the
    /// final swap-chain present. Supplying <paramref name="meshLocalBounds"/> enables the
    /// same opt-in frustum culling as the swap-chain overload, and <paramref name="meshLods"/>
    /// the same opt-in coarse LOD (see its remarks); null renders every node at full detail.</summary>
    public void Render(
        D3D12Renderer renderer,
        GpuScene gpuScene,
        IReadOnlyList<SceneNodeDraw> nodeDraws,
        IMaterialAtlas materials,
        FrameCameraSet cameras,
        LightingSetup lighting,
        PostFxSetup postFx,
        SceneRenderTarget target,
        IReadOnlyList<Aabb>? meshLocalBounds = null,
        IReadOnlyList<SceneMeshLod>? meshLods = null)
    {
        ThrowIfDisposed();
        ValidateRenderArguments(renderer, nodeDraws, materials, cameras, lighting, postFx);
        var draws = ResolveDraws(nodeDraws, meshLocalBounds, meshLods, cameras);

        var slot = renderer.SwapChain.CurrentBackBufferIndex;
        UploadSceneConstants(slot, cameras, lighting);
        var (batches, instanceBuffer) = PrepareInstances(slot, draws);

        var ctx = renderer.BeginFrame(cameras, lighting, postFx);
        try
        {
            var fg = renderer.FrameGraphConcrete;
            var colorHandle = fg.ImportTexture(target.Texture, initialState: target.InitialState);
            var scenePass = BuildPasses(
                fg, colorHandle, target.Rtv,
                target.Width, target.Height, target.FinalState,
                gpuScene, batches, instanceBuffer, materials, lighting, slot, target.ClearAlpha);
            renderer.EndFrameWithoutPresent(ctx);
            RecordDrawCounts(scenePass);
        }
        catch
        {
            renderer.EndFrameWithoutPresent(ctx);
            throw;
        }
    }

    /// <summary>Applies opt-in CPU frustum culling then opt-in coarse LOD, the two pure draw-list
    /// transforms in front of the instanced pass. With per-mesh <paramref name="meshLocalBounds"/>
    /// it builds the camera frustum from the same view-projection the scene constants use
    /// (<c>Main.View * Main.Projection</c>), drops nodes fully outside, and records
    /// <see cref="LastCulledNodeCount"/>. When <paramref name="meshLods"/> is also supplied it then
    /// reselects each surviving node to its distance-appropriate LOD level (reusing the same bounds
    /// for the camera distance) and records <see cref="LastLodDemotedNodeCount"/>. With neither
    /// supplied it returns the input list untouched and zeroes both counts, so opted-out callers
    /// render byte-for-byte as before. LOD reuses the cull bounds for distance, so it only runs when
    /// <paramref name="meshLocalBounds"/> is present.</summary>
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

    /// <summary>Groups the (already culled) draw list into per-mesh instance batches and uploads
    /// the flat instance buffer into <paramref name="slot"/>'s ring slot, returning the batch
    /// table + the buffer to bind as the pass's instance root SRV.</summary>
    private (IReadOnlyList<SceneMeshBatch> Batches, D3D12Buffer InstanceBuffer) PrepareInstances(
        uint slot, IReadOnlyList<SceneNodeDraw> draws)
    {
        var batch = SceneInstanceBatch.Build(draws);
        var instanceBuffer = _instanceRing.Upload(slot, batch.Instances);
        return (batch.Batches, instanceBuffer);
    }

    private void RecordDrawCounts(ForwardScenePass scenePass)
    {
        LastDrawnPrimitiveCount = scenePass.DrawnPrimitiveInstanceCount;
        LastDrawCallCount = scenePass.DrawCallCount;
        LastDrawnIndexCount = scenePass.DrawnIndexCount;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var cb in _sceneCbs)
        {
            cb?.Dispose();
        }

        _instanceRing.Dispose();
        _targets.Dispose();
        _tonemapPso.Dispose();
        _tonemapRootSig.Dispose();
        _scenePso.Dispose();
        _sceneRootSig.Dispose();
        _disposed = true;
    }
}
