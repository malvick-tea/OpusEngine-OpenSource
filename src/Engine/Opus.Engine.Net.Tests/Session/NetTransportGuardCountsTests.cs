using FluentAssertions;
using Opus.Engine.Net.Session;
using Opus.Engine.Net.Tests.Support;
using Xunit;

namespace Opus.Engine.Net.Tests.Session;

/// <summary>
/// The shared <see cref="NetTransportGuardCounts.FromTransport"/> seam both the session telemetry and
/// the soak harness use to fold a transport's untrusted-input guard counters: it reads the counts when
/// the transport exposes <see cref="Opus.Net.Transport.INetServerTransportDiagnostics"/> and reports
/// <see cref="NetTransportGuardCounts.None"/> otherwise.
/// </summary>
public sealed class NetTransportGuardCountsTests
{
    [Fact]
    public void FromTransport_reads_counts_from_a_capable_transport()
    {
        var transport = new StubServerDiagnosticsTransport
        {
            RejectedConnectionCount = 3,
            DroppedInboundPayloadCount = 5,
            RateLimitedInboundPayloadCount = 7,
        };

        var counts = NetTransportGuardCounts.FromTransport(transport);

        counts.Should().Be(new NetTransportGuardCounts(3, 5, 7));
    }

    [Fact]
    public void FromTransport_returns_none_for_a_null_transport()
    {
        NetTransportGuardCounts.FromTransport(null).Should().Be(NetTransportGuardCounts.None);
    }
}
