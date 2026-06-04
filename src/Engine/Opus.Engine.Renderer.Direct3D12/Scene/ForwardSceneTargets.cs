using System;
using Opus.Engine.Rhi;
using Opus.Engine.Rhi.Direct3D12;
using Silk.NET.Direct3D12;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>Lifecycle bundle for the HDR colour + depth + descriptor heaps that
/// <see cref="D3D12ForwardSceneRenderer"/> owns across frames. Sized at construction; a
/// viewport resize recreates the whole bundle (see
/// <see cref="D3D12ForwardSceneRenderer.Resize"/>) rather than mutating the immutable
/// resource handles in place.
/// <list type="bullet">
/// <item><description><see cref="HdrColor"/> — R16G16B16A16_FLOAT, sampled + render target.
///     Where <see cref="ForwardScenePass"/> writes and <see cref="TonemapPass"/> reads.</description></item>
/// <item><description><see cref="Depth"/> — D32_FLOAT, depth-stencil target only.</description></item>
/// <item><description><see cref="HdrRtvHandle"/> — bound by the forward pass as its colour target.</description></item>
/// <item><description><see cref="DsvHandle"/> — bound by the forward pass as its depth target.</description></item>
/// <item><description><see cref="HdrSrvHeap"/> + <see cref="HdrSrvTable"/> — the descriptor
///     handle <see cref="TonemapPass"/> binds to read HDR.</description></item>
/// </list></summary>
public sealed unsafe class ForwardSceneTargets : IDisposable
{
    public D3D12Texture HdrColor { get; }

    public D3D12Texture Depth { get; }

    public CpuDescriptorHandle HdrRtvHandle { get; }

    public CpuDescriptorHandle DsvHandle { get; }

    public ID3D12DescriptorHeap* HdrSrvHeap { get; }

    public GpuDescriptorHandle HdrSrvTable { get; }

    private readonly ID3D12DescriptorHeap* _rtvHeap;
    private readonly ID3D12DescriptorHeap* _dsvHeap;
    private bool _disposed;

    public ForwardSceneTargets(D3D12RhiDevice device, int width, int height, string namePrefix = "forward")
    {
        HdrColor = device.CreateGraphicsTexture(new RhiTextureDescription(
            $"{namePrefix}.hdr", width, height, 1,
            RhiTextureFormat.Rgba16Float, RhiTextureUsage.ColorTarget | RhiTextureUsage.Sampled));

        Depth = device.CreateGraphicsTexture(new RhiTextureDescription(
            $"{namePrefix}.depth", width, height, 1,
            RhiTextureFormat.D32Float, RhiTextureUsage.DepthStencilTarget));

        _rtvHeap = device.CreateRtvDescriptorHeap(1u);
        HdrRtvHandle = device.CreateRenderTargetView(HdrColor, _rtvHeap, slotIndex: 0u);

        _dsvHeap = device.CreateDsvDescriptorHeap(1u);
        DsvHandle = device.CreateDepthStencilView(Depth, _dsvHeap);

        HdrSrvHeap = device.CreateSrvDescriptorHeap(1u);
        HdrSrvTable = device.CreateShaderResourceView(HdrColor, HdrSrvHeap, slotIndex: 0u);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        HdrSrvHeap->Release();
        _dsvHeap->Release();
        _rtvHeap->Release();
        Depth.Dispose();
        HdrColor.Dispose();
        _disposed = true;
    }
}
