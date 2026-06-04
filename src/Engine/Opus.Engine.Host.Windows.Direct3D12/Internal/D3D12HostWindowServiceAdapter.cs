using System;
using Opus.Engine.Pal.Application;
using Opus.Engine.Pal.Windows.Direct3D12;

namespace Opus.Engine.Host.Windows.Direct3D12.Internal;

/// <summary>Adapts the SDL-backed window owned by <see cref="D3D12WindowSession"/> to
/// the <see cref="IWindowService"/> contract that <see cref="Opus.Engine.Runtime.OpusHost"/>
/// consumes. The adapter forwards <c>CloseRequested</c> / <c>Resized</c> / <c>Opened</c>
/// events, exposes <c>IsOpen</c> / <c>Size</c> / <c>Title</c>, and routes
/// <c>PollEvents</c> to the live SDL pump.
/// <para>
/// <c>Open</c> and <c>Close</c> are deliberately no-ops: the window lifetime belongs to
/// the surrounding <see cref="D3D12WindowSession"/>. The host opens the session before
/// constructing the adapter, and disposes the session after the host stops. This keeps
/// device / swap-chain / window teardown sequenced exactly as
/// <see cref="D3D12WindowSession.Dispose"/> requires, instead of relying on
/// <see cref="OpusHost.Stop"/> to close the SDL window directly while D3D12 resources
/// still hold it.
/// </para>
/// </summary>
internal sealed class D3D12HostWindowServiceAdapter : IWindowService
{
    private readonly D3D12WindowSession _session;
    private bool _disposed;

    public D3D12HostWindowServiceAdapter(D3D12WindowSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        _session.Window.Opened += OnOpened;
        _session.Window.CloseRequested += OnCloseRequested;
        _session.Window.Resized += OnResized;
    }

    public bool IsOpen => _session.Window.IsOpen;

    public (int Width, int Height) Size => _session.Window.Size;

    public string Title
    {
        get => _session.Window.Title;
        set => _session.Window.Title = value;
    }

    public event Action? Opened;

    public event Action? CloseRequested;

    public event Action<int, int>? Resized;

    public void Open(WindowOptions options)
    {
        // No-op: D3D12WindowSession.TryOpen already opened the underlying window.
        // Re-opening would either be ignored by the SDL backend or break the device.
    }

    public void PollEvents() => _session.Window.PollEvents();

    public void Close()
    {
        // No-op: the session owns window lifetime. OpusHost.Stop calls this during
        // shutdown; closing the SDL window here would tear it down while the D3D12
        // swap chain still references its native handle.
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _session.Window.Opened -= OnOpened;
        _session.Window.CloseRequested -= OnCloseRequested;
        _session.Window.Resized -= OnResized;
        _disposed = true;
    }

    private void OnOpened() => Opened?.Invoke();

    private void OnCloseRequested() => CloseRequested?.Invoke();

    private void OnResized(int width, int height) => Resized?.Invoke(width, height);
}
