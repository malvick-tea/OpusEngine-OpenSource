using System;
using Opus.Engine.Host.Windows.Direct3D12.Internal;
using Opus.Engine.Pal.Windows.Direct3D12;
using Opus.Engine.Runtime;
using Opus.Foundation;

namespace Opus.Engine.Host.Windows.Direct3D12;

/// <summary>Facade that opens a Windows/D3D12 alpha host in the canonical Opus 0.1
/// shape: live SDL window + D3D12 device + swap chain + DXC compiler, glTF asset
/// (consumer-supplied or procedural sample), composed scene viewport / forward renderer
/// / UI draw surface, and the <see cref="OpusHost"/> spine wired around the application.
/// Build returns a <see cref="D3D12OpusHostInstance"/> handle that the caller drives
/// through <c>host.Start</c> / <c>host.Step</c> in their event loop.</summary>
public sealed class D3D12OpusHostBuilder
{
    private D3D12OpusApplicationOptions _options = D3D12OpusApplicationOptions.Default;
    private ILog _log = NullLog.Instance;
    private OpusHostOptions? _hostOptions;

    /// <summary>Overrides the alpha host application options. Defaults to
    /// <see cref="D3D12OpusApplicationOptions.Default"/>.</summary>
    public D3D12OpusHostBuilder WithOptions(D3D12OpusApplicationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        return this;
    }

    /// <summary>Sets the <see cref="ILog"/> sink the application writes to. Defaults to
    /// <see cref="NullLog.Instance"/>; the sample host uses <see cref="ConsoleLog"/>.</summary>
    public D3D12OpusHostBuilder WithLog(ILog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        _log = log;
        return this;
    }

    /// <summary>Overrides the <see cref="OpusHostOptions"/> passed to the runtime spine.
    /// Leave unset to use the runtime defaults (fixed tick rate, render-while-paused,
    /// catch-up cap).</summary>
    public D3D12OpusHostBuilder WithHostOptions(OpusHostOptions hostOptions)
    {
        ArgumentNullException.ThrowIfNull(hostOptions);
        _hostOptions = hostOptions;
        return this;
    }

    /// <summary>Opens the D3D12 window session, loads (or generates) the alpha asset,
    /// builds the GPU rig, wires the host, and returns the composite handle. Returns
    /// <c>null</c> when the host environment cannot satisfy the prerequisites
    /// (non-Windows OS, no D3D12-capable adapter, SDL video unavailable, etc.). Callers
    /// branch on null instead of catching exceptions for the environment cases.</summary>
    public D3D12OpusHostInstance? TryBuild()
    {
        _options.Validate();

        var sessionOptions = D3D12WindowSessionOptions.Windowed(
            _options.WindowTitle,
            _options.WindowWidth,
            _options.WindowHeight,
            _options.EnableDebugLayer,
            _options.Resizable);

        var session = D3D12WindowSession.TryOpen(sessionOptions);
        if (session is null)
        {
            return null;
        }

        D3D12OpusApplication? application = null;
        D3D12HostWindowServiceAdapter? adapter = null;
        OpusHost? host = null;
        try
        {
            application = new D3D12OpusApplication(session, _options, _log);
            adapter = new D3D12HostWindowServiceAdapter(session);
            host = new OpusHost(application, _hostOptions, adapter);

            var instance = new D3D12OpusHostInstance(session, application, adapter, host);
            application = null;
            adapter = null;
            host = null;
            session = null!;
            return instance;
        }
        finally
        {
            host?.Dispose();
            application?.Dispose();
            adapter?.Dispose();
            session?.Dispose();
        }
    }
}
