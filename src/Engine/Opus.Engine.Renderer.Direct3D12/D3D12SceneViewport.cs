using System;
using Opus.Engine.Renderer;
using Opus.Engine.Renderer.Direct3D12.Scene;
using Opus.Engine.Rhi.Direct3D12;
using Silk.NET.Direct3D12;

namespace Opus.Engine.Renderer.Direct3D12;

/// <summary>Hosts a scene render that composites into a UI quad instead of presenting
/// directly. Owns an offscreen <see cref="SceneViewportTarget"/> and its own
/// <see cref="D3D12Renderer"/>; <see cref="BeginFrame"/> opens a frame on that renderer
/// and <see cref="EndFrame"/> submits the recorded work without invoking the swap chain's
/// present cycle — that responsibility belongs to the UI host that will sample the
/// viewport's <see cref="SceneViewportTarget.SrvHeap"/> on the next pass.
/// <para>
/// The constructor takes a swap chain reference solely for frame-slot indexing — the per-
/// slot command list allocators inside <see cref="D3D12Renderer"/> need a monotonic
/// rotating index that matches the host's in-flight frame count, and the host's swap chain
/// is the canonical source of truth for that. The viewport never reads back from or
/// writes to the swap chain itself.
/// </para></summary>
public sealed unsafe class D3D12SceneViewport : IDisposable
{
    private readonly D3D12RhiDevice _device;
    private readonly string _namePrefix;
    private readonly D3D12Renderer _renderer;
    private SceneViewportTarget _target;
    private bool _disposed;

    public D3D12SceneViewport(
        D3D12RhiDevice device,
        D3D12SwapChain swapChain,
        int width,
        int height,
        string namePrefix = "scene-viewport")
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(swapChain);

        _device = device;
        _namePrefix = namePrefix;
        _target = new SceneViewportTarget(device, width, height, namePrefix);
        _renderer = new D3D12Renderer(device, swapChain, $"{namePrefix}.renderer");
    }

    public SceneViewportTarget Target => _target;

    /// <summary>The dedicated <see cref="D3D12Renderer"/> driving this viewport's frame.
    /// Exposed for scene-pass authors that need
    /// <see cref="D3D12Renderer.FrameGraphConcrete"/> between <see cref="BeginFrame"/>
    /// and <see cref="EndFrame"/>; the host's primary renderer (the one driving the swap
    /// chain) belongs to the host, not to the viewport.</summary>
    public D3D12Renderer Renderer => _renderer;

    /// <summary>Opens a scene frame on the viewport's renderer. Mirrors
    /// <see cref="D3D12Renderer.BeginFrame"/>; pass authors run between this call and
    /// <see cref="EndFrame"/> via the typed <see cref="D3D12Renderer.FrameGraphConcrete"/>.</summary>
    public IFrameContext BeginFrame(FrameCameraSet cameras, LightingSetup lighting, PostFxSetup postFx)
    {
        ThrowIfDisposed();
        return _renderer.BeginFrame(cameras, lighting, postFx);
    }

    /// <summary>Closes the scene frame without presenting. The recorded work submits to
    /// the GPU queue immediately — the UI pass that samples <see cref="SceneViewportTarget.SrvHeap"/>
    /// runs after this on the same queue, so the offscreen colour is fully written by the
    /// time the UI's pixel shader reads it.</summary>
    public void EndFrame(IFrameContext context)
    {
        ThrowIfDisposed();
        _renderer.EndFrameWithoutPresent(context);
    }

    /// <summary>Builds the <see cref="SceneRenderTarget"/> descriptor that
    /// <see cref="D3D12ForwardSceneRenderer"/>'s offscreen-target overload consumes.
    /// Resting state is <see cref="ResourceStates.PixelShaderResource"/> — the target
    /// stays sampler-ready between frames so the UI's composite quad reads a coherent
    /// image without a per-frame transition the viewport doesn't own.
    /// <para>
    /// <c>ClearAlpha = 0f</c>: the forward pass clears sky pixels to transparent so the
    /// UI surface composites the quad over chrome drawn earlier in the frame. Mesh
    /// pixels write opaque alpha and read out at full coverage; sky pixels are punched
    /// through by alpha-blending in <see cref="D3D12DrawSurface.DrawTexturedRect"/>.
    /// </para></summary>
    public SceneRenderTarget CreateRenderTargetDescriptor()
    {
        ThrowIfDisposed();
        return new SceneRenderTarget(
            _target.Color,
            _target.RtvHandle,
            _target.Width,
            _target.Height,
            _target.Format,
            InitialState: ResourceStates.PixelShaderResource,
            FinalState: ResourceStates.PixelShaderResource,
            ClearAlpha: 0f);
    }

    /// <summary>Recreates the offscreen colour target (and its RTV / SRV heaps) at the new
    /// size, preserving the dedicated <see cref="D3D12Renderer"/> — which is size-agnostic
    /// and imports the target afresh each frame, so the next frame samples the new texture.
    /// The new target is built before the old one is released, so a failed allocation leaves
    /// the viewport on its previous target. No-op on a degenerate or unchanged size. The
    /// caller must have drained the GPU first.</summary>
    public void Resize(int width, int height)
    {
        ThrowIfDisposed();
        if (width <= 0 || height <= 0 || (width == _target.Width && height == _target.Height))
        {
            return;
        }

        var newTarget = new SceneViewportTarget(_device, width, height, _namePrefix);
        _target.Dispose();
        _target = newTarget;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _renderer.Dispose();
        _target.Dispose();
        _disposed = true;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
