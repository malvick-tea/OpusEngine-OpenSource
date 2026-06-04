using System;
using Opus.Engine.Pal.Application;
using Opus.Engine.Pal.Windows.Direct3D12;
using Opus.Engine.Runtime;

namespace Opus.Engine.Host.Windows.Direct3D12;

/// <summary>Composite handle returned by <see cref="D3D12OpusHostBuilder"/>. Owns the
/// open <see cref="D3D12WindowSession"/>, the <see cref="D3D12OpusApplication"/>, the
/// <see cref="IWindowService"/> adapter, and the <see cref="OpusHost"/> spine wrapping
/// them.
/// <para>
/// User code typically calls <see cref="OpusHost.Start"/>, drives <see cref="OpusHost.Step"/>
/// in a loop, and disposes the instance once the loop exits. <see cref="Dispose"/>
/// stops the host (which signals <c>OnStopping</c> to the application), then tears down
/// the application's GPU rig, the IWindowService adapter, and the D3D12 session in the
/// order each resource requires.
/// </para>
/// </summary>
public sealed class D3D12OpusHostInstance : IDisposable
{
    private readonly IWindowService _windowAdapter;
    private bool _disposed;

    internal D3D12OpusHostInstance(
        D3D12WindowSession session,
        D3D12OpusApplication application,
        IWindowService windowAdapter,
        OpusHost host)
    {
        Session = session;
        Application = application;
        _windowAdapter = windowAdapter;
        Host = host;
    }

    public OpusHost Host { get; }

    public D3D12OpusApplication Application { get; }

    public D3D12WindowSession Session { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Host.Dispose();
        Application.Dispose();
        _windowAdapter.Dispose();
        Session.Dispose();
        _disposed = true;
    }
}
