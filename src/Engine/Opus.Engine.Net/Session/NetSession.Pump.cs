using System;
using System.Globalization;
using Opus.Net.Transport;

namespace Opus.Engine.Net.Session;

/// <summary>
/// Per-tick event drain, peer-membership bookkeeping, and reconnect advance for
/// <see cref="NetSession"/>. Pulled out of the main partial so the public surface and
/// lifecycle methods stay in one focused file; this companion holds the busy state-
/// machine bridge between transport-level <see cref="NetEvent"/> and session-level
/// <see cref="NetSessionEvent"/>. Mirrors the partial-class split used by
/// <c>UdpServerTransport.Dispatch.cs</c>.
/// </summary>
public sealed partial class NetSession
{
    private void DrainTransport(Action<NetSessionEvent>? eventHandler)
    {
        if (_transport is null)
        {
            return;
        }

        try
        {
            _transport.Poll(_scratch);
        }
        catch (Exception ex)
        {
            RecordFault(NetSessionFaultCode.TransportException, "Transport.Poll threw.", ex);
            EmitFaultEvent(eventHandler);
            return;
        }

        foreach (var transportEvent in _scratch)
        {
            HandleTransportEvent(transportEvent, eventHandler);
        }

        if (_state == NetSessionState.Connected && _peers.Count == 0)
        {
            HandleAllPeersLost(eventHandler);
        }
    }

    private void HandleTransportEvent(NetEvent transportEvent, Action<NetSessionEvent>? eventHandler)
    {
        switch (transportEvent.Kind)
        {
            case NetEventKind.Connected:
                HandlePeerConnected(transportEvent.Connection, eventHandler);
                break;
            case NetEventKind.Disconnected:
                HandlePeerDisconnected(transportEvent.Connection, eventHandler);
                break;
            case NetEventKind.Received:
                EnqueueReceived(transportEvent, eventHandler);
                break;
        }
    }

    private void HandlePeerConnected(ConnectionId peer, Action<NetSessionEvent>? eventHandler)
    {
        if (!_peers.Add(peer))
        {
            return;
        }

        _statistics.RecordPeerAccepted();
        _state = NetSessionState.Connected;
        _reconnect?.Reset();
        Emit(eventHandler, NetSessionEvent.ForPeer(
            NetSessionEventKind.PeerConnected,
            peer,
            connectedPeerCount: _peers.Count,
            UtcNow(),
            diagnosticCode: NetDiagnosticCodes.SessionPeerConnected));
    }

    private void HandlePeerDisconnected(ConnectionId peer, Action<NetSessionEvent>? eventHandler)
    {
        if (!_peers.Remove(peer))
        {
            return;
        }

        _statistics.RecordPeerDisconnected();
        Emit(eventHandler, NetSessionEvent.ForPeer(
            NetSessionEventKind.PeerDisconnected,
            peer,
            connectedPeerCount: _peers.Count,
            UtcNow(),
            diagnosticCode: NetDiagnosticCodes.SessionPeerDisconnected));
    }

    private void EnqueueReceived(NetEvent transportEvent, Action<NetSessionEvent>? eventHandler)
    {
        if (!_peers.Contains(transportEvent.Connection))
        {
            return;
        }

        var payload = transportEvent.Payload ?? Array.Empty<byte>();
        if (_receiveQueue.Enqueue(transportEvent.Connection, payload))
        {
            _statistics.RecordQueuedPayloadDropped();
        }

        _statistics.RecordPacketReceived(payload.Length);
        Emit(eventHandler, NetSessionEvent.ForPeer(
            NetSessionEventKind.PayloadReceived,
            transportEvent.Connection,
            connectedPeerCount: _peers.Count,
            UtcNow(),
            payloadByteCount: payload.Length));
    }

    private void HandleAllPeersLost(Action<NetSessionEvent>? eventHandler)
    {
        if (_options.Role == NetSessionRole.Server)
        {
            _state = NetSessionState.Connecting;
            return;
        }

        var reconnect = _reconnect!;
        if (!reconnect.HasBudget)
        {
            RecordFault(NetSessionFaultCode.ReconnectBudgetExhausted, "Reconnect budget exhausted.", exception: null);
            Emit(eventHandler, NetSessionEvent.ForLifecycle(
                NetSessionEventKind.ReconnectExhausted,
                connectedPeerCount: 0,
                UtcNow(),
                detail: _lastFault?.Detail,
                diagnosticCode: NetDiagnosticCodes.SessionReconnectExhausted));
            return;
        }

        var delay = reconnect.Schedule();
        _state = NetSessionState.Reconnecting;
        TearDownTransport();
        Emit(eventHandler, NetSessionEvent.ForLifecycle(
            NetSessionEventKind.ReconnectScheduled,
            connectedPeerCount: 0,
            UtcNow(),
            detail: delay.ToString("c", CultureInfo.InvariantCulture),
            diagnosticCode: NetDiagnosticCodes.SessionReconnectScheduled));
    }

    private void AdvanceReconnect(TimeSpan elapsed, Action<NetSessionEvent>? eventHandler)
    {
        var reconnect = _reconnect!;
        reconnect.Advance(elapsed);
        if (!reconnect.ShouldFireNow)
        {
            return;
        }

        var attempt = reconnect.RecordAttempt();
        _statistics.RecordReconnectAttempt();
        Emit(eventHandler, NetSessionEvent.ForLifecycle(
            NetSessionEventKind.ReconnectAttempted,
            connectedPeerCount: 0,
            UtcNow(),
            detail: attempt.ToString(CultureInfo.InvariantCulture),
            diagnosticCode: NetDiagnosticCodes.SessionReconnectAttempted));

        if (TryCreateTransport(eventHandler))
        {
            _state = NetSessionState.Connecting;
        }
    }

    private void DrainOnStop(Action<NetSessionEvent>? eventHandler)
    {
        DrainTransport(eventHandler);
        TearDownTransport();
        _peers.Clear();
        _receiveQueue.Clear();
        _state = NetSessionState.Idle;
        _stopRequested = false;
        Emit(eventHandler, NetSessionEvent.ForLifecycle(
            NetSessionEventKind.Stopped,
            connectedPeerCount: 0,
            UtcNow(),
            diagnosticCode: NetDiagnosticCodes.SessionStopped));
    }
}
