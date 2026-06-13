using System;
using FluentAssertions;
using Opus.Engine.Net.Session;
using Opus.Engine.Net.Telemetry;
using Opus.Engine.Net.Tests.Support;
using Opus.Foundation;
using Opus.Net.Transport;
using Xunit;

namespace Opus.Engine.Net.Tests.Telemetry;

/// <summary>Behaviour of <see cref="NetSessionLogSink"/>: maps event kind to log level,
/// emits stable code-prefixed lines, and respects the underlying log's minimum-level
/// filter so trace-level payload traffic does not flood production logs.</summary>
public sealed class NetSessionLogSinkTests
{
    [Fact]
    public void Peer_connected_logs_at_Information_level_with_diagnostic_code()
    {
        var log = new CollectingLog();
        var sink = new NetSessionLogSink(log);

        sink.Observe(NetSessionEvent.ForPeer(
            NetSessionEventKind.PeerConnected,
            new ConnectionId(7UL),
            connectedPeerCount: 1,
            DateTimeOffset.UtcNow,
            diagnosticCode: NetDiagnosticCodes.SessionPeerConnected));

        log.Entries.Should().ContainSingle();
        log.Entries[0].Level.Should().Be(LogLevel.Information);
        log.Entries[0].Message.Should().Contain(NetDiagnosticCodes.SessionPeerConnected);
        log.Entries[0].Message.Should().Contain("PeerConnected");
    }

    [Fact]
    public void Reconnect_scheduled_logs_at_Warning_level()
    {
        var log = new CollectingLog();
        var sink = new NetSessionLogSink(log);

        sink.Observe(NetSessionEvent.ForLifecycle(
            NetSessionEventKind.ReconnectScheduled,
            connectedPeerCount: 0,
            DateTimeOffset.UtcNow,
            detail: "00:00:00.25",
            diagnosticCode: NetDiagnosticCodes.SessionReconnectScheduled));

        log.Entries.Should().ContainSingle();
        log.Entries[0].Level.Should().Be(LogLevel.Warning);
    }

    [Fact]
    public void Reconnect_exhausted_logs_at_Error_level()
    {
        var log = new CollectingLog();
        var sink = new NetSessionLogSink(log);

        sink.Observe(NetSessionEvent.ForLifecycle(
            NetSessionEventKind.ReconnectExhausted,
            connectedPeerCount: 0,
            DateTimeOffset.UtcNow,
            diagnosticCode: NetDiagnosticCodes.SessionReconnectExhausted));

        log.Entries.Should().ContainSingle();
        log.Entries[0].Level.Should().Be(LogLevel.Error);
    }

    [Fact]
    public void Payload_received_below_minimum_level_is_suppressed()
    {
        var log = new CollectingLog { MinimumLevel = LogLevel.Information };
        var sink = new NetSessionLogSink(log);

        sink.Observe(NetSessionEvent.ForPeer(
            NetSessionEventKind.PayloadReceived,
            new ConnectionId(7UL),
            connectedPeerCount: 1,
            DateTimeOffset.UtcNow,
            payloadByteCount: 128));

        log.Entries.Should().BeEmpty();
    }
}
