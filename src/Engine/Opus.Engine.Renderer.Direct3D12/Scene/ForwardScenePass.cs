using System.Collections.Generic;
using System.Numerics;
using Opus.Engine.FrameGraph;
using Opus.Engine.Renderer.Direct3D12.Assets;
using Opus.Engine.Rhi;
using Opus.Engine.Rhi.Direct3D12;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>Canonical opaque forward pass for <see cref="D3D12ForwardSceneRenderer"/>, GPU
/// instanced: clears the HDR target + depth, binds the scene constant buffer + albedo
/// descriptor heap from the supplied <see cref="IMaterialAtlas"/> + the per-frame instance
/// <see cref="GpuInstanceData"/> buffer (root SRV), then walks the <see cref="SceneMeshBatch"/>
/// table emitting one <c>DrawIndexedInstanced</c> per mesh primitive that fans across every
/// instance of that mesh. The vertex shader reads each instance's world + tint from the buffer
/// at <c>InstanceOffset + SV_InstanceID</c>. The pass leaves the HDR target ready to be sampled
/// by <see cref="TonemapPass"/>.
/// <para>
/// Owned by the scene renderer; not intended for direct use from game code. The renderer
/// reconstructs a fresh pass per frame because the back-buffer / scene-CB / batch table / instance
/// buffer rotate every frame.
/// </para></summary>
public sealed class ForwardScenePass : D3D12RenderPass
{
    private const uint VertexStride = 32u;

    private readonly FrameGraphResource _hdrTarget;
    private readonly FrameGraphResource _depthTarget;
    private readonly D3D12RootSignature _rootSig;
    private readonly D3D12GraphicsPipeline _pso;
    private readonly D3D12Buffer _sceneCb;
    private readonly D3D12Buffer _instanceBuffer;
    private readonly CpuDescriptorHandle _hdrRtv;
    private readonly CpuDescriptorHandle _dsv;
    private readonly GpuScene _gpuScene;
    private readonly IReadOnlyList<SceneMeshBatch> _batches;
    private readonly IMaterialAtlas _atlas;
    private readonly Vector3 _clearColor;
    private readonly float _clearAlpha;
    private readonly int _width;
    private readonly int _height;

    /// <summary>Number of indexed draw calls the pass issued — one per mesh primitive per batch,
    /// independent of instance count. This is the instancing win metric: with N repeated copies
    /// of a mesh it stays at that mesh's primitive count instead of N times it.</summary>
    public int DrawCallCount { get; private set; }

    /// <summary>Total primitive-instances rendered — the sum over batches of
    /// (instance count × mesh primitive count). Equals the pre-instancing per-draw primitive
    /// total, so culling that removes instances still lowers it.</summary>
    public int DrawnPrimitiveInstanceCount { get; private set; }

    /// <summary>Total indices submitted this pass — the sum over batches of
    /// (instance count × mesh index count). The triangle-budget metric coarse LOD reduces:
    /// reselecting a distant instance to a lower-index mesh variant lowers this without changing
    /// the draw-call or primitive-instance count.</summary>
    public int DrawnIndexCount { get; private set; }

    public ForwardScenePass(
        FrameGraphResource hdrTarget,
        FrameGraphResource depthTarget,
        D3D12RootSignature rootSig,
        D3D12GraphicsPipeline pso,
        D3D12Buffer sceneCb,
        D3D12Buffer instanceBuffer,
        CpuDescriptorHandle hdrRtv,
        CpuDescriptorHandle dsv,
        GpuScene gpuScene,
        IReadOnlyList<SceneMeshBatch> batches,
        IMaterialAtlas atlas,
        Vector3 clearColor,
        int width,
        int height,
        float clearAlpha = 1f)
    {
        _hdrTarget = hdrTarget;
        _depthTarget = depthTarget;
        _rootSig = rootSig;
        _pso = pso;
        _sceneCb = sceneCb;
        _instanceBuffer = instanceBuffer;
        _hdrRtv = hdrRtv;
        _dsv = dsv;
        _gpuScene = gpuScene;
        _batches = batches;
        _atlas = atlas;
        _clearColor = clearColor;
        _clearAlpha = clearAlpha;
        _width = width;
        _height = height;
    }

    public override string Name => "ForwardScene";

    public override void Setup(D3D12FrameGraphBuilder builder)
    {
        builder.ColorTarget(_hdrTarget);
        builder.DepthTarget(_depthTarget);
    }

    public override void Execute(D3D12RenderPassContext context)
    {
        var cmd = context.CommandList;
        cmd.OMSetRenderTarget(_hdrRtv, _dsv);
        cmd.RSSetViewport(_width, _height);
        cmd.RSSetScissorRect(_width, _height);
        cmd.ClearRenderTargetView(_hdrRtv, _clearColor.X, _clearColor.Y, _clearColor.Z, _clearAlpha);
        cmd.ClearDepthStencilView(_dsv, depth: 1.0f);

        _atlas.BindHeapTo(cmd);
        cmd.SetGraphicsRootSignature(_rootSig);
        cmd.SetPipelineState(_pso);
        cmd.SetGraphicsRootConstantBufferView(rootParameterIndex: 0u, _sceneCb);
        cmd.SetGraphicsRootShaderResourceView(rootParameterIndex: 3u, _instanceBuffer);
        cmd.IASetTriangleListTopology();

        var drawCalls = 0;
        var primitiveInstances = 0;
        var indices = 0;
        foreach (var batch in _batches)
        {
            var slice = _gpuScene.SlicesByMesh[batch.MeshIndex];
            for (var p = 0; p < slice.Count; p++)
            {
                var prim = _gpuScene.Primitives[slice.Start + p];
                var material = _atlas.Resolve(prim.MaterialIndex);
                cmd.SetGraphicsRootDescriptorTable(rootParameterIndex: 2u, material.MapTable);

                // Per-primitive material factors (base colour + metallic/roughness + emissive) and
                // this batch's instance-buffer offset. The vertex shader multiplies the base-colour
                // factor by each instance's own tint; the pixel shader multiplies each sampled PBR
                // map by its matching factor.
                var drawConstants = new InstancedDrawConstants
                {
                    MaterialFactor = material.BaseColorFactor,
                    MetalRoughness = new Vector4(material.MetallicFactor, material.RoughnessFactor, 0f, 0f),
                    EmissiveFactor = new Vector4(material.EmissiveFactor, 0f),
                    InstanceOffset = (uint)batch.InstanceOffset,
                };
                cmd.SetGraphicsRoot32BitConstants(
                    rootParameterIndex: 1u,
                    numValues: InstancedDrawConstants.Num32BitValues,
                    in drawConstants);
                cmd.IASetVertexBuffer(prim.Vb, VertexStride);
                cmd.IASetIndexBuffer(prim.Ib, Format.FormatR32Uint);
                cmd.DrawIndexedInstanced(prim.IndexCount, (uint)batch.InstanceCount);
                drawCalls++;
                primitiveInstances += batch.InstanceCount;
                indices += (int)prim.IndexCount * batch.InstanceCount;
            }
        }

        DrawCallCount = drawCalls;
        DrawnPrimitiveInstanceCount = primitiveInstances;
        DrawnIndexCount = indices;
    }
}
