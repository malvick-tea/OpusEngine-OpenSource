using Opus.Engine.Rhi.Direct3D12;
using Silk.NET.Direct3D12;

namespace Opus.Engine.Pal.Windows.Direct3D12;

/// <summary>
/// Per-frame context returned by <see cref="D3D12UiFrameLoop.BeginFrame"/>. Carries the
/// open command list, the swap-chain's current RTV handle, the back-buffer slot index,
/// and the viewport size — exactly the surface area <c>D3D12DrawSurface.BeginFrame</c>
/// needs from a host.
///
/// <para>Immutable + struct so the per-frame allocation cost is zero. The frame's
/// lifetime is bracketed by the loop's <c>BeginFrame</c> / <c>EndFrame</c> pair; the
/// host owns the resources, the caller borrows them through this value.</para>
/// </summary>
public readonly struct D3D12UiFrame
{
    public D3D12UiFrame(
        D3D12CommandList commandList,
        CpuDescriptorHandle renderTargetView,
        int backBufferSlot,
        int viewportWidth,
        int viewportHeight)
    {
        CommandList = commandList;
        RenderTargetView = renderTargetView;
        BackBufferSlot = backBufferSlot;
        ViewportWidth = viewportWidth;
        ViewportHeight = viewportHeight;
    }

    public D3D12CommandList CommandList { get; }

    public CpuDescriptorHandle RenderTargetView { get; }

    public int BackBufferSlot { get; }

    public int ViewportWidth { get; }

    public int ViewportHeight { get; }
}
