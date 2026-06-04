using System;
using Opus.Net.Transport;

namespace Opus.Engine.Net.Session;

/// <summary>
/// Cumulative untrusted-input guard counters read from a server transport that implements
/// <see cref="INetServerTransportDiagnostics"/>, folded into <see cref="NetSessionStatisticsSnapshot"/>
/// so the alpha's overlay and failure report can show the connection-flood, queue-cap, and per-peer
/// rate-limit guards firing instead of leaving a tester to guess why payloads went missing.
/// </summary>
/// <param name="RejectedConnections">Inbound connection requests rejected by the concurrent-peer cap.</param>
/// <param name="DroppedInboundPayloads">Inbound payloads shed because the shared receive queue was full.</param>
/// <param name="RateLimitedInboundPayloads">Inbound payloads shed by the per-peer rate limiter.</param>
public readonly record struct NetTransportGuardCounts(
    long RejectedConnections,
    long DroppedInboundPayloads,
    long RateLimitedInboundPayloads)
{
    /// <summary>The all-zero counts reported for a client session, the in-process loopback transport,
    /// or any transport that does not implement <see cref="INetServerTransportDiagnostics"/>.</summary>
    public static NetTransportGuardCounts None { get; }

    /// <summary>Reads a fresh set of counts from a transport's diagnostics capability.</summary>
    public static NetTransportGuardCounts From(INetServerTransportDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        return new NetTransportGuardCounts(
            diagnostics.RejectedConnectionCount,
            diagnostics.DroppedInboundPayloadCount,
            diagnostics.RateLimitedInboundPayloadCount);
    }

    /// <summary>Reads the guard counts from <paramref name="transport"/> when it exposes the optional
    /// <see cref="INetServerTransportDiagnostics"/> capability, or <see cref="None"/> when it does not
    /// (a client / loopback transport) or is null. The single seam both <c>NetSession</c> and the soak
    /// harness use to fold a transport's untrusted-input guards into their telemetry.</summary>
    public static NetTransportGuardCounts FromTransport(INetTransport? transport) =>
        transport is INetServerTransportDiagnostics diagnostics ? From(diagnostics) : None;
}
