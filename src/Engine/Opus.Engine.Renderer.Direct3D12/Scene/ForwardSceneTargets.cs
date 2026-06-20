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
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "MA0055:Do not use finalizer",
    Justification = "The finalizer releases only unmanaged descriptor heaps.")]
public sealed unsafe class ForwardSceneTargets : IDisposable
{
    public D3D12Texture HdrColor { get; }

    public D3D12Texture Depth { get; }

    public CpuDescriptorHandle HdrRtvHandle { get; }

    public CpuDescriptorHandle DsvHandle { get; }

    public ID3D12DescriptorHeap* HdrSrvHeap { get; private set; }

    public GpuDescriptorHandle HdrSrvTable { get; }

    private ID3D12DescriptorHeap* _rtvHeap;
    private ID3D12DescriptorHeap* _dsvHeap;
    private bool _disposed;

    ~ForwardSceneTargets()
    {
        ReleaseHeaps();
    }

    public ForwardSceneTargets(D3D12RhiDevice device, int width, int height, string namePrefix = "forward")
    {
        D3D12Texture? hdrColor = null;
        D3D12Texture? depth = null;
        ID3D12DescriptorHeap* rtvHeap = null;
        ID3D12DescriptorHeap* dsvHeap = null;
        ID3D12DescriptorHeap* srvHeap = null;
        try
        {
            hdrColor = device.CreateGraphicsTexture(new RhiTextureDescription(
                $"{namePrefix}.hdr", width, height, 1,
                RhiTextureFormat.Rgba16Float, RhiTextureUsage.ColorTarget | RhiTextureUsage.Sampled));
            depth = device.CreateGraphicsTexture(new RhiTextureDescription(
                $"{namePrefix}.depth", width, height, 1,
                RhiTextureFormat.D32Float, RhiTextureUsage.DepthStencilTarget));

            rtvHeap = device.CreateRtvDescriptorHeap(1u);
            HdrRtvHandle = device.CreateRenderTargetView(hdrColor, rtvHeap, slotIndex: 0u);
            dsvHeap = device.CreateDsvDescriptorHeap(1u);
            DsvHandle = device.CreateDepthStencilView(depth, dsvHeap);
            srvHeap = device.CreateSrvDescriptorHeap(1u);
            HdrSrvTable = device.CreateShaderResourceView(hdrColor, srvHeap, slotIndex: 0u);

            HdrColor = hdrColor;
            Depth = depth;
            _rtvHeap = rtvHeap;
            _dsvHeap = dsvHeap;
            HdrSrvHeap = srvHeap;
            rtvHeap = null;
            dsvHeap = null;
            srvHeap = null;
        }
        catch
        {
            if (srvHeap != null)
            {
                srvHeap->Release();
            }

            if (dsvHeap != null)
            {
                dsvHeap->Release();
            }

            if (rtvHeap != null)
            {
                rtvHeap->Release();
            }

            depth?.Dispose();
            hdrColor?.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
        ReleaseHeaps();
        Depth.Dispose();
        HdrColor.Dispose();
    }

    private void ReleaseHeaps()
    {
        if (HdrSrvHeap != null)
        {
            HdrSrvHeap->Release();
            HdrSrvHeap = null;
        }

        if (_dsvHeap != null)
        {
            _dsvHeap->Release();
            _dsvHeap = null;
        }

        if (_rtvHeap != null)
        {
            _rtvHeap->Release();
            _rtvHeap = null;
        }
    }
}
