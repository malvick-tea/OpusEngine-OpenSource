using Silk.NET.Direct3D12;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Output-merger and rasterizer-stage setup: bind RT/DSV pairs, clear them,
/// pin viewport + scissor. The canonical "open a render pass" preamble for the forward
/// pipeline and every post-process pass.</summary>
public sealed unsafe partial class D3D12CommandList
{
    /// <summary>Binds a single colour render target with no depth buffer. Use
    /// <see cref="OMSetRenderTarget(CpuDescriptorHandle, CpuDescriptorHandle)"/> for the depth-attached variant.</summary>
    public void OMSetRenderTarget(CpuDescriptorHandle rtv)
    {
        _commandList->OMSetRenderTargets(1u, &rtv, RTsSingleHandleToDescriptorRange: 1, pDepthStencilDescriptor: null);
    }

    /// <summary>Binds a single colour render target + a depth-stencil target. Both must
    /// reference live descriptors in heaps owned by the caller.</summary>
    public void OMSetRenderTarget(CpuDescriptorHandle rtv, CpuDescriptorHandle dsv)
    {
        _commandList->OMSetRenderTargets(1u, &rtv, RTsSingleHandleToDescriptorRange: 1, pDepthStencilDescriptor: &dsv);
    }

    /// <summary>Clears the bound render target referenced by <paramref name="rtv"/> to
    /// the supplied colour. Called between Begin / End in the typical
    /// transition → clear → transition pattern for swap chain frames.</summary>
    public void ClearRenderTargetView(CpuDescriptorHandle rtv, float r, float g, float b, float a)
    {
        var colour = stackalloc float[4] { r, g, b, a };
        _commandList->ClearRenderTargetView(rtv, colour, 0u, pRects: null);
    }

    /// <summary>Clears the depth target referenced by <paramref name="dsv"/> to
    /// <paramref name="depth"/> (typically 1.0 for reversed-Z-naïve depth tests).</summary>
    public void ClearDepthStencilView(CpuDescriptorHandle dsv, float depth = 1.0f, byte stencil = 0)
    {
        _commandList->ClearDepthStencilView(
            dsv,
            ClearFlags.Depth,
            depth,
            stencil,
            NumRects: 0,
            pRects: null);
    }

    /// <summary>Sets a full-viewport rectangle covering 0,0 → width,height with depth 0..1.</summary>
    public void RSSetViewport(int width, int height)
    {
        var vp = new Viewport
        {
            TopLeftX = 0f,
            TopLeftY = 0f,
            Width = width,
            Height = height,
            MinDepth = 0f,
            MaxDepth = 1f,
        };
        _commandList->RSSetViewports(1u, &vp);
    }

    /// <summary>Sets a full-window scissor rect — every pixel inside the viewport draws.</summary>
    public void RSSetScissorRect(int width, int height)
    {
        var rect = new Silk.NET.Maths.Box2D<int>(0, 0, width, height);
        _commandList->RSSetScissorRects(1u, &rect);
    }
}
