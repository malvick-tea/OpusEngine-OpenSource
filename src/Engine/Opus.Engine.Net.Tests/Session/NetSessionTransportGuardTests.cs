using System;
using FluentAssertions;
using Opus.Engine.Net.Session;
using Opus.Engine.Net.Tests.Support;
using Xunit;

namespace Opus.Engine.Net.Tests.Session;

/// <summary>
/// The session statistics snapshot folds the underlying transport's untrusted-input guard counters
/// (connection-flood rejects, inbound-queue-cap drops, per-peer rate-limit sheds) when the transport
/// exposes the optional <see cref="Opus.Net.Transport.INetServerTransportDiagnostics"/> capability,
/// and reports zero when it does not.
/// </summary>
public sealed class NetSessionTransportGuardTests
{
    [Fact]
    public void Server_session_folds_transport_guard_counts_into_the_statistics_snapshot()
    {
        var transport = new StubServerDiagnosticsTransport
        {
            RejectedConnectionCount = 3,
            DroppedInboundPayloadCount = 5,
            RateLimitedInboundPayloadCount = 7,
        };
        using var session = NetSession.AdoptServer(
            new NetSessionOptions(NetSessionRole.Server, "srv"),
            transport);
        session.Tick(TimeSpan.Zero);

        var guards = session.Statistics.TransportGuards;

        guards.RejectedConnections.Should().Be(3);
        guards.DroppedInboundPayloads.Should().Be(5);
        guards.RateLimitedInboundPayloads.Should().Be(7);
    }

    [Fact]
    public void Client_session_reports_no_transport_guard_counts()
    {
        using var factory = new LoopbackClientTransportFactory();
        using var session = NetSession.Client(
            new NetSessionOptions(NetSessionRole.Client, "cli", NetReconnectPolicy.Disabled),
            factory);
        session.Tick(TimeSpan.Zero);

        // The loopback transport does not implement the diagnostics capability, so the session must
        // report the all-zero counts rather than guessing or throwing.
        session.Statistics.TransportGuards.Should().Be(NetTransportGuardCounts.None);
    }
}
