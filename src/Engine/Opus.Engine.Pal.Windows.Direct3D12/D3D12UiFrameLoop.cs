using System;
using Opus.Engine.Rhi.Direct3D12;
using Silk.NET.Direct3D12;

namespace Opus.Engine.Pal.Windows.Direct3D12;

/// <summary>
/// Idiomatic frame loop wrapper for a UI-only D3D12 host: opens the command list per
/// frame, transitions the swap-chain back-buffer Present→RenderTarget, hands the caller
/// a <see cref="D3D12UiFrame"/> to record into, then drives the
/// RenderTarget→Present transition + close + execute + Present on <see cref="EndFrame"/>.
///
/// <para>Owns only the per-host command list — the underlying window / device / swap
/// chain belong to the supplied <see cref="D3D12WindowSession"/>. One frame loop per
/// session; multi-viewport rendering is out of scope.</para>
///
/// <para>vsync uses sync-interval 0 (no wait) — matches the demo and avoids stalling the
/// UI thread on the OS compositor while early development iteration is the priority.
/// Production toggle moves into <see cref="D3D12WindowSessionOptions"/> when the
/// Settings → Video tab gets wired through (post-Phase-B).</para>
/// </summary>
public sealed unsafe class D3D12UiFrameLoop : IDisposable
{
    private const uint PresentSyncIntervalImmediate = 0u;
    private const string CommandListDebugName = "engine.pal.windows.d3d12.uiframe";

    private readonly D3D12WindowSession _session;
    private readonly D3D12CommandList _commandList;
    private bool _inFrame;
    private uint _activeSlot;
    private bool _disposed;

    public D3D12UiFrameLoop(D3D12WindowSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        _commandList = session.Device.CreateGraphicsCommandList(CommandListDebugName);
    }

    public D3D12WindowSession Session => _session;

    public D3D12CommandList CommandList => _commandList;

    /// <summary>True between matched <see cref="BeginFrame"/> / <see cref="EndFrame"/>
    /// calls. Diagnostic surface — production code should bracket frames with the
    /// methods directly rather than poll this.</summary>
    public bool IsFrameOpen => _inFrame;

    /// <summary>Opens the per-slot allocator, transitions the swap-chain back buffer
    /// Present→RenderTarget, and returns the frame context the caller records into.
    /// Idempotent against double-open: throws on the second consecutive call without a
    /// matching <see cref="EndFrame"/>.</summary>
    public D3D12UiFrame BeginFrame()
    {
        ThrowIfDisposed();
        if (_inFrame)
        {
            throw new InvalidOperationException(
                "BeginFrame called without a matching EndFrame for the previous frame.");
        }

        _activeSlot = _session.SwapChain.CurrentBackBufferIndex;
        _commandList.Begin(_activeSlot);
        _commandList.ResourceBarrierTransition(
            _session.SwapChain.CurrentBackBuffer,
            ResourceStates.Present,
            ResourceStates.RenderTarget);

        _inFrame = true;
        return new D3D12UiFrame(
            _commandList,
            _session.SwapChain.CurrentRenderTargetView,
            (int)_activeSlot,
            _session.SwapChain.Width,
            _session.SwapChain.Height);
    }

    /// <summary>Transitions the back buffer RenderTarget→Present, closes + executes the
    /// command list, and presents. Caller is responsible for pumping
    /// <see cref="Opus.Engine.Pal.Sdl3.SdlWindowService.PollEvents"/> at its preferred
    /// cadence — the frame loop is render-only by design so the host can reorder input
    /// polling vs. rendering for latency tuning.</summary>
    public void EndFrame()
    {
        ThrowIfDisposed();
        if (!_inFrame)
        {
            throw new InvalidOperationException("EndFrame called without a matching BeginFrame.");
        }

        _commandList.ResourceBarrierTransition(
            _session.SwapChain.CurrentBackBuffer,
            ResourceStates.RenderTarget,
            ResourceStates.Present);
        _commandList.End();
        _commandList.ExecuteOn(_session.Device);
        _session.SwapChain.Present(PresentSyncIntervalImmediate);
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
            _session.Device.WaitForIdle();
        }
        catch
        {
            // Best-effort drain — see D3D12WindowSession.Dispose for the same comment.
        }

        _commandList.Dispose();
        _disposed = true;
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);
}
