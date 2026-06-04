using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentAssertions;
using Opus.Engine.Net.Session;
using Opus.Engine.Net.Tests.Support;
using Xunit;

namespace Opus.Engine.Net.Tests.Session;

/// <summary>End-to-end behavior of <see cref="NetSession"/> driven over a loopback
/// transport. Covers the join, drain, send, and disconnect contracts so the engine-level
/// session shape is pinned independently of which transport implementation lands underneath.</summary>
public sealed class NetSessionLoopbackTests
{
    [Fact]
    public void Client_session_starts_emits_Started_event_with_diagnostic_code()
    {
        using var factory = new LoopbackClientTransportFactory();
        using var session = NetSession.Client(
            new NetSessionOptions(NetSessionRole.Client, "client-A", NetReconnectPolicy.Disabled),
            factory);
        var events = new List<NetSessionEvent>();

        session.Tick(TimeSpan.Zero, events.Add);

        session.State.Should().Be(NetSessionState.Connected);
        events.Should().ContainSingle(e => e.Kind == NetSessionEventKind.Started)
            .Which.DiagnosticCode.Should().Be(NetDiagnosticCodes.SessionStarted);
        events.Should().Contain(e => e.Kind == NetSessionEventKind.PeerConnected
            && e.DiagnosticCode == NetDiagnosticCodes.SessionPeerConnected);
    }

    [Fact]
    public void Send_returns_false_until_session_observes_peer_connected()
    {
        using var factory = new LoopbackClientTransportFactory();
        using var session = NetSession.Client(
            new NetSessionOptions(NetSessionRole.Client, "client-A", NetReconnectPolicy.Disabled),
            factory);

        session.Send(factory.LastLink?.ServerPeerId ?? default, new byte[] { 1, 2, 3 })
            .Should().BeFalse();
        session.Statistics.PacketsSendDropped.Should().Be(1);
    }

    [Fact]
    public void Receive_queue_drains_through_NextReceivedPayload()
    {
        using var factory = new LoopbackClientTransportFactory();
        using var session = NetSession.Client(
            new NetSessionOptions(NetSessionRole.Client, "client-A", NetReconnectPolicy.Disabled),
            factory);
        session.Tick(TimeSpan.Zero);
        var link = factory.LastLink!;
        var payload = Encoding.UTF8.GetBytes("hello-from-server");
        link.Server.Send(link.ClientPeerId, payload);

        session.Tick(TimeSpan.Zero);

        session.NextReceivedPayload(out var from, out var received).Should().BeTrue();
        received.Should().Equal(payload);
        from.IsValid.Should().BeTrue();
        session.NextReceivedPayload(out _, out _).Should().BeFalse();
        session.Statistics.PacketsReceived.Should().Be(1);
        session.Statistics.BytesReceived.Should().Be(payload.Length);
    }

    [Fact]
    public void Send_succeeds_once_session_is_Connected_and_increments_counter()
    {
        using var factory = new LoopbackClientTransportFactory();
        using var session = NetSession.Client(
            new NetSessionOptions(NetSessionRole.Client, "client-A", NetReconnectPolicy.Disabled),
            factory);
        session.Tick(TimeSpan.Zero);
        var link = factory.LastLink!;

        var payload = new byte[] { 9, 9, 9, 9 };
        session.Send(link.ServerPeerId, payload).Should().BeTrue();
        session.Statistics.PacketsSent.Should().Be(1);
        session.Statistics.BytesSent.Should().Be(payload.Length);
    }

    [Fact]
    public void Disconnect_from_peer_surfaces_PeerDisconnected_event_and_clears_state()
    {
        using var factory = new LoopbackClientTransportFactory();
        using var session = NetSession.Client(
            new NetSessionOptions(NetSessionRole.Client, "client-A", NetReconnectPolicy.Disabled),
            factory);
        session.Tick(TimeSpan.Zero);
        var link = factory.LastLink!;
        var events = new List<NetSessionEvent>();

        link.Server.Disconnect(link.ClientPeerId);
        session.Tick(TimeSpan.Zero, events.Add);

        events.Should().Contain(e => e.Kind == NetSessionEventKind.PeerDisconnected);
        session.Statistics.PeersDisconnectedTotal.Should().Be(1);
    }

    [Fact]
    public void RequestStop_drains_then_emits_Stopped()
    {
        using var factory = new LoopbackClientTransportFactory();
        using var session = NetSession.Client(
            new NetSessionOptions(NetSessionRole.Client, "client-A", NetReconnectPolicy.Disabled),
            factory);
        session.Tick(TimeSpan.Zero);
        var events = new List<NetSessionEvent>();

        session.RequestStop();
        session.Tick(TimeSpan.Zero, events.Add);

        session.State.Should().Be(NetSessionState.Idle);
        events.Should().Contain(e => e.Kind == NetSessionEventKind.Stopped
            && e.DiagnosticCode == NetDiagnosticCodes.SessionStopped);
    }

    [Fact]
    public void Server_role_adopts_pre_bound_transport()
    {
        var link = Opus.Net.Loopback.LoopbackTransportPair.Create();
        using var session = NetSession.AdoptServer(
            new NetSessionOptions(NetSessionRole.Server, "server-A"),
            link.Server);
        var events = new List<NetSessionEvent>();

        session.Tick(TimeSpan.Zero, events.Add);

        session.Role.Should().Be(NetSessionRole.Server);
        events.Should().ContainSingle(e => e.Kind == NetSessionEventKind.Started);
        events.Should().Contain(e => e.Kind == NetSessionEventKind.PeerConnected);
        link.Client.Dispose();
    }

    [Fact]
    public void Statistics_snapshot_captures_observed_state()
    {
        using var factory = new LoopbackClientTransportFactory();
        using var session = NetSession.Client(
            new NetSessionOptions(NetSessionRole.Client, "stats", NetReconnectPolicy.Disabled),
            factory);
        session.Tick(TimeSpan.Zero);

        var snapshot = session.Statistics;
        snapshot.ConnectedPeerCount.Should().Be(1);
        snapshot.PeersAcceptedTotal.Should().Be(1);
        snapshot.PacketsReceived.Should().Be(0);
        snapshot.ObservedAtUtc.Should().BeAfter(default);
    }

    [Fact]
    public void Send_to_unknown_peer_drops_and_does_not_throw()
    {
        using var factory = new LoopbackClientTransportFactory();
        using var session = NetSession.Client(
            new NetSessionOptions(NetSessionRole.Client, "client-A", NetReconnectPolicy.Disabled),
            factory);
        session.Tick(TimeSpan.Zero);

        session.Send(new Opus.Net.Transport.ConnectionId(424242UL), new byte[] { 1 })
            .Should().BeFalse();
        session.Statistics.PacketsSendDropped.Should().Be(1);
    }

    [Fact]
    public void Dispose_drains_and_marks_state_Disposed()
    {
        using var factory = new LoopbackClientTransportFactory();
        var session = NetSession.Client(
            new NetSessionOptions(NetSessionRole.Client, "client-A", NetReconnectPolicy.Disabled),
            factory);
        session.Tick(TimeSpan.Zero);

        session.Dispose();
        session.State.Should().Be(NetSessionState.Disposed);
        session.Send(default, Array.Empty<byte>()).Should().BeFalse();
    }
}
