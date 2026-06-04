namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Command-list + swap-chain factory delegates. Thin partial — the actual implementations
/// live in <see cref="D3D12CommandList"/> / <see cref="D3D12SwapChain"/>.</summary>
public sealed unsafe partial class D3D12RhiDevice
{
    public IRhiCommandList CreateCommandList(string debugName) =>
        D3D12CommandList.Create(_device, debugName);

    /// <summary>Backend-typed command list. <paramref name="frameSlots"/> defaults to
    /// <see cref="D3D12SwapChain.BufferCount"/> so the per-back-buffer allocator pattern
    /// just works.</summary>
    public D3D12CommandList CreateGraphicsCommandList(string debugName, int frameSlots = D3D12SwapChain.BufferCount) =>
        D3D12CommandList.Create(_device, debugName, frameSlots);

    /// <summary>Creates a DXGI swap chain on the given HWND.</summary>
    public D3D12SwapChain CreateSwapChain(System.IntPtr hwnd, int width, int height) =>
        D3D12SwapChain.Create(this, hwnd, width, height);
}
