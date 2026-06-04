using System.Runtime.InteropServices;
using Opus.Engine.FrameGraph;
using Opus.Engine.Rhi.Direct3D12;
using Silk.NET.Direct3D12;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>Canonical HDR→LDR tonemap pass for <see cref="D3D12ForwardSceneRenderer"/>:
/// a bufferless fullscreen triangle sampling the HDR target, applying Narkowicz ACES
/// filmic + gamma 2.2 encode, and writing to the swap-chain backbuffer.
/// <para>
/// Reads <see cref="ForwardSceneTargets.HdrColor"/>; writes the imported backbuffer
/// resource. No depth I/O — depth was finalised by <see cref="ForwardScenePass"/> and is
/// not consulted by tonemap.
/// </para></summary>
public sealed unsafe class TonemapPass : D3D12RenderPass
{
    private readonly FrameGraphResource _backBuffer;
    private readonly FrameGraphResource _hdrSource;
    private readonly D3D12RootSignature _rootSig;
    private readonly D3D12GraphicsPipeline _pso;
    private readonly CpuDescriptorHandle _backBufferRtv;
    private readonly ID3D12DescriptorHeap* _hdrSrvHeap;
    private readonly GpuDescriptorHandle _hdrSrvTable;
    private readonly int _width;
    private readonly int _height;

    public TonemapPass(
        FrameGraphResource backBuffer,
        FrameGraphResource hdrSource,
        D3D12RootSignature rootSig,
        D3D12GraphicsPipeline pso,
        CpuDescriptorHandle backBufferRtv,
        ID3D12DescriptorHeap* hdrSrvHeap,
        GpuDescriptorHandle hdrSrvTable,
        int width,
        int height)
    {
        _backBuffer = backBuffer;
        _hdrSource = hdrSource;
        _rootSig = rootSig;
        _pso = pso;
        _backBufferRtv = backBufferRtv;
        _hdrSrvHeap = hdrSrvHeap;
        _hdrSrvTable = hdrSrvTable;
        _width = width;
        _height = height;
    }

    public override string Name => "Tonemap";

    public override void Setup(D3D12FrameGraphBuilder builder)
    {
        builder.ColorTarget(_backBuffer);
        builder.Read(_hdrSource);
    }

    public override void Execute(D3D12RenderPassContext context)
    {
        var cmd = context.CommandList;
        cmd.OMSetRenderTarget(_backBufferRtv);
        cmd.RSSetViewport(_width, _height);
        cmd.RSSetScissorRect(_width, _height);

        cmd.SetDescriptorHeaps(_hdrSrvHeap);
        cmd.SetGraphicsRootSignature(_rootSig);
        cmd.SetPipelineState(_pso);
        cmd.SetGraphicsRoot32BitConstants(rootParameterIndex: 0u, numValues: 4u, in TonemapRootConstants);
        cmd.SetGraphicsRootDescriptorTable(rootParameterIndex: 1u, _hdrSrvTable);
        cmd.IASetTriangleListTopology();
        cmd.DrawInstanced(vertexCount: 3u, instanceCount: 1u);
    }

    /// <summary>4-DWORD constants slot consumed by <see cref="TonemapShaders.PixelShader"/>.
    /// Reserved for exposure + colour-grading knobs; all zero at this milestone (ACES alone,
    /// no HDR-exposure scaling — that lands with the PostFxSetup.ExposureEv follow-up).</summary>
    private static TonemapConstants TonemapRootConstants;

    /// <summary>4-float scratch for the tonemap's <c>b0</c> root constants.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct TonemapConstants
    {
        public float Exposure;
        public float Reserved0;
        public float Reserved1;
        public float Reserved2;
    }
}
