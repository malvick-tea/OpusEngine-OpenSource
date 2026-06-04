using System;
using Opus.Net.Transport;

namespace Opus.Engine.Net.Session;

/// <summary>
/// Engine-level network session contract. Wraps a single
/// <see cref="INetTransport"/> in a state machine that tracks peer membership, accumulates
/// traffic statistics, surfaces structured <see cref="NetSessionEvent"/> notifications,
/// and (client role only) drives reconnect attempts through a transport factory.
/// <para>
/// The session is single-threaded: <see cref="Tick"/>, <see cref="Send"/>,
/// <see cref="Disconnect"/>, and <see cref="RequestStop"/> all run on the caller's tick
/// thread (typically the same thread as the game/host loop). Statistics and state may be
/// observed safely on the same thread without locks.
/// </para>
/// </summary>
public interface INetSession : IDisposable
{
    /// <summary>Stable display name surfaced in logs and telemetry rows.</summary>
    string DisplayName { get; }

    /// <summary>Role this session was constructed for. Reconnect logic is only active
    /// when this is <see cref="NetSessionRole.Client"/>.</summary>
    NetSessionRole Role { get; }

    /// <summary>Current state machine position.</summary>
    NetSessionState State { get; }

    /// <summary>Last fault recorded against this session; <c>null</c> while
    /// <see cref="State"/> never crossed into <see cref="NetSessionState.Faulted"/>.</summary>
    NetSessionFault? LastFault { get; }

    /// <summary>Returns a snapshot of the rolling statistics.</summary>
    NetSessionStatisticsSnapshot Statistics { get; }

    /// <summary>Records a single round-trip-time sample observed by a consumer-side
    /// ping protocol. The session aggregates samples into a bounded rolling window
    /// surfaced inside <see cref="NetSessionStatisticsSnapshot.Rtt"/>; the engine does
    /// not measure RTT itself because the ping shape is genre-specific. Throws
    /// <see cref="ArgumentOutOfRangeException"/> when <paramref name="rtt"/> is
    /// negative.</summary>
    void RecordRtt(TimeSpan rtt);

    /// <summary>Sends a payload through the underlying transport. Returns false when the
    /// session is not in <see cref="NetSessionState.Connected"/>, when the peer is
    /// unknown, or when the transport refuses the buffer. Increments the appropriate
    /// statistics counter on every call (success or rejected).</summary>
    bool Send(ConnectionId target, ReadOnlySpan<byte> payload);

    /// <summary>Requests a clean disconnect of the specified peer. No-op when the peer is
    /// not connected; the resulting Disconnected event is surfaced on the next
    /// <see cref="Tick"/> if the transport echoes one back.</summary>
    void Disconnect(ConnectionId target);

    /// <summary>Pops the next received payload off the bounded receive queue. Returns
    /// false (and produces <see cref="ConnectionId.None"/> / empty payload) when no
    /// payloads remain. The byte buffer is owned by the caller after return.</summary>
    bool NextReceivedPayload(out ConnectionId from, out byte[] payload);

    /// <summary>Asks the session to leave whatever state it is in by tearing the
    /// transport down cleanly and transitioning to <see cref="NetSessionState.Idle"/>.
    /// Subsequent ticks pump the final transport events and then emit
    /// <see cref="NetSessionEventKind.Stopped"/>.</summary>
    void RequestStop();

    /// <summary>Advances the session state machine by <paramref name="elapsed"/>. Drains
    /// the underlying transport, updates statistics, queues received payloads, and emits
    /// every observed <see cref="NetSessionEvent"/> through
    /// <paramref name="eventHandler"/>. The callback is optional; if null, events are
    /// silently observed and only state/statistics reflect the changes.</summary>
    void Tick(TimeSpan elapsed, Action<NetSessionEvent>? eventHandler = null);
}
