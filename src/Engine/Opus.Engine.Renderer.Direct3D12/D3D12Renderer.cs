using System;
using Opus.Engine.FrameGraph;
using Opus.Engine.Renderer;
using Opus.Engine.Rhi;
using Opus.Engine.Rhi.Direct3D12;

namespace Opus.Engine.Renderer.Direct3D12;

/// <summary>
/// Direct3D 12 implementation of <see cref="IRenderer"/>. Owns a frame graph + command list
/// against a caller-supplied <see cref="D3D12RhiDevice"/> and <see cref="D3D12SwapChain"/>;
/// <see cref="BeginFrame"/> resets the graph and opens the command list, <see cref="EndFrame"/>
/// compiles + executes + closes + submits + presents.
/// </summary>
/// <remarks>
/// M3-wrap.a scope:
/// <list type="bullet">
/// <item><description>Bridges <see cref="IRenderer"/> + <see cref="IFrameGraph"/> + <see cref="IRhiDevice"/>
///     contracts to the live D3D12 stack.</description></item>
/// <item><description>Frame lifecycle: BeginFrame → consumer adds passes via <see cref="FrameGraphConcrete"/>
///     → EndFrame compiles, executes, presents.</description></item>
/// <item><description>Device / swap-chain ownership stays with the caller (test harness, host).
///     Renderer owns only the command list + the frame graph instance it created.</description></item>
/// </list>
/// Out of scope (lands in later milestones): abstract <c>IRhi*</c> resource bridging, the
/// canonical Forward+ pass set, scene extract / renderable plumbing, multi-view rendering.
/// </remarks>
public sealed class D3D12Renderer : IRenderer
{
    private readonly D3D12RhiDevice _device;
    private readonly D3D12SwapChain _swapChain;
    private readonly D3D12FrameGraph _frameGraph;
    private readonly D3D12CommandList _commandList;
    private readonly D3D12RhiDeviceAdapter _deviceAdapter;
    private readonly D3D12FrameGraphAdapter _frameGraphAdapter;

    private ulong _frameIndex;
    private bool _inFrame;
    private uint _activeSlot;
    private bool _disposed;

    /// <summary>Constructs a renderer over an externally-owned device + swap chain. The
    /// renderer creates its own frame graph + command list and disposes both on
    /// <see cref="Dispose"/>; the caller retains ownership of device + swap chain.</summary>
    /// <param name="device">Live D3D12 device (the singleton held by
    /// <c>D3D12TestHarness</c> in tests; a host-owned instance in runtime).</param>
    /// <param name="swapChain">The presentation target. One swap chain per renderer
    /// today; multi-window support arrives via multi-viewport.</param>
    /// <param name="commandListDebugName">Diagnostic name on the command list — surfaces
    /// in PIX captures and crash dumps.</param>
    public D3D12Renderer(D3D12RhiDevice device, D3D12SwapChain swapChain, string commandListDebugName = "d3d12-renderer")
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _swapChain = swapChain ?? throw new ArgumentNullException(nameof(swapChain));
        _frameGraph = new D3D12FrameGraph();
        _commandList = device.CreateGraphicsCommandList(commandListDebugName);
        _deviceAdapter = new D3D12RhiDeviceAdapter(_device);
        _frameGraphAdapter = new D3D12FrameGraphAdapter(_frameGraph, _commandList);
    }

    public IRhiDevice Device => _deviceAdapter;

    public IFrameGraph FrameGraph => _frameGraphAdapter;

    /// <summary>Escape hatch for typed access to the underlying D3D12 device — required by
    /// passes that allocate D3D12-specific resources (buffers, textures, descriptor heaps)
    /// until the abstract RHI surface covers those concerns.</summary>
    public D3D12RhiDevice DeviceConcrete => _device;

    /// <summary>Escape hatch for adding <see cref="D3D12RenderPass"/>es to the frame graph
    /// — the concrete passes carry the typed command-list surface their <c>Execute</c>
    /// implementations need.</summary>
    public D3D12FrameGraph FrameGraphConcrete => _frameGraph;

    public D3D12CommandList CommandList => _commandList;

    public D3D12SwapChain SwapChain => _swapChain;

    /// <summary>Index of the frame currently in flight. Starts at 0 before the first
    /// <see cref="BeginFrame"/>, increments on each call.</summary>
    public ulong FrameIndex => _frameIndex;

    public IFrameContext BeginFrame(FrameCameraSet cameras, LightingSetup lighting, PostFxSetup postFx)
    {
        ThrowIfDisposed();
        if (_inFrame)
        {
            throw new InvalidOperationException("BeginFrame called without a matching EndFrame for the previous frame.");
        }

        _frameIndex++;
        _activeSlot = _swapChain.CurrentBackBufferIndex;
        _frameGraph.Reset();
        _commandList.Begin(_activeSlot);
        _inFrame = true;

        return new D3D12FrameContext(cameras, lighting, postFx, _frameIndex);
    }

    public void EndFrame(IFrameContext context)
    {
        EndFrameInternal(context, present: true);
    }

    /// <summary>Closes the frame without invoking <see cref="D3D12SwapChain.Present"/>.
    /// Used by hosts that own the present cycle elsewhere (e.g. when the scene renders
    /// into an offscreen target that a separate UI pass composites, leaving the actual
    /// swap-chain present to that UI pass). Compiles + executes the frame graph and
    /// submits the command list exactly like <see cref="EndFrame"/>; only the trailing
    /// <c>Present</c> is skipped.</summary>
    public void EndFrameWithoutPresent(IFrameContext context)
    {
        EndFrameInternal(context, present: false);
    }

    private void EndFrameInternal(IFrameContext context, bool present)
    {
        ThrowIfDisposed();
        if (!_inFrame)
        {
            throw new InvalidOperationException("EndFrame called without a matching BeginFrame.");
        }

        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        _frameGraph.Compile();
        _frameGraph.Execute(_commandList);
        _commandList.End();
        _commandList.ExecuteOn(_device);
        if (present)
        {
            _swapChain.Present(syncInterval: 0u);
        }

        _inFrame = false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _device.WaitForIdle();
        }
        catch
        {
            // Best-effort drain before tearing the renderer's owned objects. If the device
            // is already gone we can't do anything useful here.
            // 67 SIX SEVEN 
        }

        _frameGraph.Dispose();
        _commandList.Dispose();
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(D3D12Renderer));
        }
    }
}
