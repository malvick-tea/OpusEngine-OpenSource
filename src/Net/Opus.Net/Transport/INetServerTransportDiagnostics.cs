namespace Opus.Net.Transport;

/// <summary>
/// Optional capability a server-side <see cref="INetTransport"/> may implement to surface its
/// untrusted-input guard counters to the engine layer: connection-flood rejects, inbound-queue-cap
/// drops, and per-peer rate-limit sheds. The base <see cref="INetTransport"/> contract stays
/// datagram-minimal, so a consumer that wants this observability tests for the capability with
/// <c>is</c> / <c>as</c> and reads zero when the transport does not implement it (the in-process
/// loopback transport and the single-peer client transport do not).
/// </summary>
/// <remarks>
/// Every counter is cumulative since the transport instance was created (a reconnect that builds a
/// new transport restarts them) and is safe to read from any thread. The values mirror the concrete
/// guard counters the transport already exposes; this interface only makes them reachable through a
/// transport-neutral seam so a higher layer need not reference a specific transport assembly.
/// </remarks>
public interface INetServerTransportDiagnostics
{
    /// <summary>Cumulative inbound connection requests the transport rejected because the
    /// concurrent-peer table was already full — the connection-flood DoS guard.</summary>
    long RejectedConnectionCount { get; }

    /// <summary>Cumulative inbound payloads shed because the shared receive queue was already at its
    /// cap — the queue-memory guard. A slow consumer or a flood can both drive this.</summary>
    long DroppedInboundPayloadCount { get; }

    /// <summary>Cumulative inbound payloads shed by the per-peer inbound rate limiter because a single
    /// peer exceeded its burst and sustained rate — the per-peer fairness guard, independent of how
    /// full the shared queue is.</summary>
    long RateLimitedInboundPayloadCount { get; }
}
