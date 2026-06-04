using Opus.Engine.Renderer.Direct3D12.Alpha;

namespace Opus.Engine.Host.Windows.Direct3D12;

public sealed unsafe partial class D3D12OpusApplication
{
    /// <summary>Resizes the live render surface to a new window client size: rebuilds the
    /// swap-chain back buffers, then regenerates the scene viewport / forward-renderer
    /// offscreen targets and the alpha-frame plan (camera aspect + composite rect) for the
    /// new size — reaching the exact state a host started at that size would have. The
    /// swap-chain resize drains the GPU, and the rig rebuild drains again before touching
    /// its resources.
    /// <para>
    /// Wired to the window's <c>Resized</c> event through <see cref="Pal.Windows.Direct3D12.D3D12WindowResizeBridge"/>,
    /// which fires during <c>OpusHost.Step</c>'s <c>PollEvents</c> — so a resize is applied
    /// between frames on the device thread, never mid-frame. A no-op after <see cref="Dispose"/>,
    /// or for a size below the alpha-frame minimum (e.g. a minimised window reporting a tiny
    /// or zero client area): the host keeps its last valid surface until a usable size returns.
    /// </para></summary>
    public void Resize(int width, int height)
    {
        if (_disposed
            || width < D3D12AlphaFramePlan.MinimumBackBufferWidth
            || height < D3D12AlphaFramePlan.MinimumBackBufferHeight)
        {
            return;
        }

        _session.SwapChain.Resize(width, height);
        _rig.Resize(width, height);
    }
}
