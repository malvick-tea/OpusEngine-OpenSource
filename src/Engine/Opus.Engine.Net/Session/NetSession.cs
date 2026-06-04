using System;
using System.Collections.Generic;
using Opus.Net.Transport;

namespace Opus.Engine.Net.Session;

/// <summary>
/// Concrete engine-level session orchestrator. See <see cref="INetSession"/> for the
/// public contract. Long-lived state is split across collaborators —
/// <see cref="NetSessionStatistics"/> owns the rolling counters,
/// <see cref="NetSessionReceiveQueue"/> owns the bounded payload buffer, and
/// <see cref="NetSessionReconnectController"/> owns the reconnect cadence — so this class
/// stays under the tutorial's preferred class-size cap and the state-machine work is the
/// only thing it has to read top to bottom. Per-tick event drain logic lives in
/// <c>NetSession.Pump.cs</c> following the partial-class precedent established by
/// <c>UdpServerTransport.Dispatch.cs</c> for similarly-scoped engine concerns.
/// </summary>
public sealed partial class NetSession : INetSession
{
    private readonly NetSessionOptions _options;
    private readonly INetSessionTransportFactory? _factory;
    private readonly List<NetEvent> _scratch = new();
    private readonly NetSessionReceiveQueue _receiveQueue;
    private readonly HashSet<ConnectionId> _peers = new();
    private readonly NetSessionStatistics _statistics = new();
    private readonly NetSessionReconnectController? _reconnect;
    private readonly TimeProvider _time;

    private INetTransport? _transport;
    private NetSessionState _state;
    private NetSessionFault? _lastFault;
    private bool _stopRequested;

    private NetSession(
        NetSessionOptions options,
        INetSessionTransportFactory? factory,
        INetTransport? initialTransport,
        TimeProvider time)
    {
        _options = options;
        _factory = factory;
        _transport = initialTransport;
        _time = time;
        _receiveQueue = new NetSessionReceiveQueue(options.MaxQueuedPayloads);
        _reconnect = options.Role == NetSessionRole.Client
            ? new NetSessionReconnectController(options.EffectiveReconnect)
            : null;
        _state = NetSessionState.Idle;
    }

    /// <summary>Creates a client session that builds its transport (and every reconnect
    /// transport) through <paramref name="factory"/>.</summary>
    public static NetSession Client(
        NetSessionOptions options,
        INetSessionTransportFactory factory,
        TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(factory);
        options.Validate();
        if (options.Role != NetSessionRole.Client)
        {
            throw new ArgumentException("NetSession.Client requires options.Role == Client.", nameof(options));
        }

        return new NetSession(options, factory, initialTransport: null, time ?? TimeProvider.System);
    }

    /// <summary>Creates a server session that adopts <paramref name="boundTransport"/>.
    /// The session takes ownership of the transport and disposes it on shutdown.</summary>
    public static NetSession AdoptServer(
        NetSessionOptions options,
        INetTransport boundTransport,
        TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(boundTransport);
        options.Validate();
        if (options.Role != NetSessionRole.Server)
        {
            throw new ArgumentException("NetSession.AdoptServer requires options.Role == Server.", nameof(options));
        }

        return new NetSession(options, factory: null, initialTransport: boundTransport, time ?? TimeProvider.System);
    }

    public string DisplayName => _options.DisplayName;

    public NetSessionRole Role => _options.Role;

    public NetSessionState State => _state;

    public NetSessionFault? LastFault => _lastFault;

    public NetSessionStatisticsSnapshot Statistics =>
        _statistics.Snapshot(_peers.Count, NetTransportGuardCounts.FromTransport(_transport), UtcNow());

    public void RecordRtt(TimeSpan rtt) => _statistics.RecordRtt(rtt);

    public bool Send(ConnectionId target, ReadOnlySpan<byte> payload)
    {
        if (_state != NetSessionState.Connected || _transport is null || !_peers.Contains(target))
        {
            _statistics.RecordSendDropped();
            return false;
        }

        bool accepted;
        try
        {
            accepted = _transport.Send(target, payload);
        }
        catch (Exception ex)
        {
            RecordFault(NetSessionFaultCode.TransportException, "Transport.Send threw.", ex);
            return false;
        }

        if (accepted)
        {
            _statistics.RecordPacketSent(payload.Length);
        }
        else
        {
            _statistics.RecordSendDropped();
        }

        return accepted;
    }

    public void Disconnect(ConnectionId target)
    {
        if (_transport is null || _state is NetSessionState.Idle or NetSessionState.Disposed)
        {
            return;
        }

        try
        {
            _transport.Disconnect(target);
        }
        catch (Exception ex)
        {
            RecordFault(NetSessionFaultCode.TransportException, "Transport.Disconnect threw.", ex);
        }
    }

    public bool NextReceivedPayload(out ConnectionId from, out byte[] payload) =>
        _receiveQueue.TryDequeue(out from, out payload);

    public void RequestStop() => _stopRequested = true;

    public void Tick(TimeSpan elapsed, Action<NetSessionEvent>? eventHandler = null)
    {
        if (_state == NetSessionState.Disposed)
        {
            return;
        }

        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        if (_state == NetSessionState.Idle && !_stopRequested)
        {
            BootstrapTransport(eventHandler);
        }

        if (_stopRequested)
        {
            DrainOnStop(eventHandler);
            return;
        }

        if (_state == NetSessionState.Reconnecting)
        {
            AdvanceReconnect(elapsed, eventHandler);
        }

        DrainTransport(eventHandler);
    }

    public void Dispose()
    {
        if (_state == NetSessionState.Disposed)
        {
            return;
        }

        TearDownTransport();
        _state = NetSessionState.Disposed;
        _peers.Clear();
        _receiveQueue.Clear();
    }

    private void BootstrapTransport(Action<NetSessionEvent>? eventHandler)
    {
        if (_transport is null && !TryCreateTransport(eventHandler))
        {
            return;
        }

        _reconnect?.Reset();
        _state = NetSessionState.Connecting;
        Emit(eventHandler, NetSessionEvent.ForLifecycle(
            NetSessionEventKind.Started,
            connectedPeerCount: 0,
            UtcNow(),
            detail: _options.DisplayName,
            diagnosticCode: NetDiagnosticCodes.SessionStarted));
    }

    private bool TryCreateTransport(Action<NetSessionEvent>? eventHandler)
    {
        if (_factory is null)
        {
            RecordFault(NetSessionFaultCode.TransportInvalidState, "Server session has no transport bound.", exception: null);
            EmitFaultEvent(eventHandler);
            return false;
        }

        try
        {
            _transport = _factory.Create();
        }
        catch (Exception ex)
        {
            RecordFault(NetSessionFaultCode.ReconnectFactoryThrew, "Transport factory threw.", ex);
            EmitFaultEvent(eventHandler);
            return false;
        }

        if (_transport is null)
        {
            RecordFault(NetSessionFaultCode.ReconnectFactoryThrew, "Transport factory returned null.", exception: null);
            EmitFaultEvent(eventHandler);
            return false;
        }

        return true;
    }

    private void TearDownTransport()
    {
        var transport = _transport;
        _transport = null;
        if (transport is null)
        {
            return;
        }

        try
        {
            transport.Dispose();
        }
        catch (Exception ex)
        {
            RecordFault(NetSessionFaultCode.TransportException, "Transport.Dispose threw.", ex);
        }
    }

    private void RecordFault(NetSessionFaultCode code, string detail, Exception? exception)
    {
        _lastFault = exception is null
            ? NetSessionFault.FromDetail(code, detail, UtcNow())
            : NetSessionFault.FromException(code, detail, UtcNow(), exception);
        _state = NetSessionState.Faulted;
        TearDownTransport();
    }

    private void EmitFaultEvent(Action<NetSessionEvent>? eventHandler) => Emit(
        eventHandler,
        NetSessionEvent.ForLifecycle(
            NetSessionEventKind.TransportFault,
            connectedPeerCount: 0,
            UtcNow(),
            detail: _lastFault?.Detail,
            diagnosticCode: NetDiagnosticCodes.SessionTransportFault));

    private static void Emit(Action<NetSessionEvent>? eventHandler, NetSessionEvent sessionEvent) =>
        eventHandler?.Invoke(sessionEvent);

    private DateTimeOffset UtcNow() => _time.GetUtcNow();
}
