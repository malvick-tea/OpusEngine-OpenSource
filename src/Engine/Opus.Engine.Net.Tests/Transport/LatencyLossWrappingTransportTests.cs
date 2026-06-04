using System;
using System.Collections.Generic;
using FluentAssertions;
using Opus.Engine.Net.Transport;
using Opus.Net.Loopback;
using Opus.Net.Transport;
using Xunit;

namespace Opus.Engine.Net.Tests.Transport;

public sealed class LatencyLossWrappingTransportTests
{
    [Fact]
    public void Ctor_null_inner_throws()
    {
        var act = () => new LatencyLossWrappingTransport(null!, LatencyLossInjectionProfile.None);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_null_profile_throws()
    {
        using var hub = LoopbackTransportHub.Create();
        var client = hub.Accept("client").Client;

        var act = () => new LatencyLossWrappingTransport(client, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Pass_through_profile_forwards_send_immediately()
    {
        using var hub = LoopbackTransportHub.Create();
        var client = hub.Accept("client").Client;
        using var wrapper = new LatencyLossWrappingTransport(client, LatencyLossInjectionProfile.None);
        var events = new List<NetEvent>();

        var sent = wrapper.Send(LoopbackTransportHub.HubSentinelId, new byte[] { 1, 2, 3 });
        wrapper.Poll(events);
        hub.Poll(events);

        sent.Should().BeTrue();
        wrapper.DroppedPacketCount.Should().Be(0);
        wrapper.DelayedPacketCount.Should().Be(0);
    }

    [Fact]
    public void Loss_rate_of_one_drops_every_packet_but_returns_true()
    {
        using var hub = LoopbackTransportHub.Create();
        var client = hub.Accept("client").Client;
        var profile = new LatencyLossInjectionProfile(LossRate: 1.0, AddedLatency: TimeSpan.Zero, Seed: 1);
        using var wrapper = new LatencyLossWrappingTransport(client, profile);

        wrapper.Send(LoopbackTransportHub.HubSentinelId, new byte[] { 1 });
        wrapper.Send(LoopbackTransportHub.HubSentinelId, new byte[] { 2 });

        wrapper.DroppedPacketCount.Should().Be(2);
        wrapper.PendingScheduledSendCount.Should().Be(0);
    }

    [Fact]
    public void Latency_injection_queues_sends_until_deadline()
    {
        using var hub = LoopbackTransportHub.Create();
        var client = hub.Accept("client").Client;
        var time = new TestTimeProvider();
        var profile = new LatencyLossInjectionProfile(LossRate: 0.0, AddedLatency: TimeSpan.FromMilliseconds(50), Seed: 0);
        using var wrapper = new LatencyLossWrappingTransport(client, profile, time);
        var serverEvents = new List<NetEvent>();
        var clientEvents = new List<NetEvent>();
        // Drain the initial Connected event the hub emits at accept time.
        hub.Poll(serverEvents);
        serverEvents.Clear();

        wrapper.Send(LoopbackTransportHub.HubSentinelId, new byte[] { 7, 7, 7 });
        wrapper.Poll(clientEvents);
        hub.Poll(serverEvents);
        ReceivedEvents(serverEvents).Should().BeEmpty();
        wrapper.DelayedPacketCount.Should().Be(1);
        wrapper.PendingScheduledSendCount.Should().Be(1);

        time.Advance(TimeSpan.FromMilliseconds(60));
        serverEvents.Clear();
        wrapper.Poll(clientEvents);
        hub.Poll(serverEvents);

        wrapper.PendingScheduledSendCount.Should().Be(0);
        ReceivedEvents(serverEvents).Should().ContainSingle();
    }

    private static IEnumerable<NetEvent> ReceivedEvents(IEnumerable<NetEvent> events)
    {
        foreach (var evt in events)
        {
            if (evt.Kind == NetEventKind.Received)
            {
                yield return evt;
            }
        }
    }

    [Fact]
    public void Deterministic_seed_reproduces_drop_pattern()
    {
        var profile = new LatencyLossInjectionProfile(LossRate: 0.5, AddedLatency: TimeSpan.Zero, Seed: 12345);
        var firstPattern = CapturePattern(profile, packetCount: 16);
        var secondPattern = CapturePattern(profile, packetCount: 16);

        secondPattern.Should().Equal(firstPattern);
    }

    [Fact]
    public void Different_seeds_produce_different_drop_patterns()
    {
        var profileA = new LatencyLossInjectionProfile(LossRate: 0.5, AddedLatency: TimeSpan.Zero, Seed: 1);
        var profileB = new LatencyLossInjectionProfile(LossRate: 0.5, AddedLatency: TimeSpan.Zero, Seed: 9999);
        var patternA = CapturePattern(profileA, packetCount: 32);
        var patternB = CapturePattern(profileB, packetCount: 32);

        patternA.Should().NotEqual(patternB);
    }

    [Fact]
    public void Disconnect_forwards_to_inner_transport_without_throwing()
    {
        using var hub = LoopbackTransportHub.Create();
        var accept = hub.Accept("client");
        using var wrapper = new LatencyLossWrappingTransport(accept.Client, LatencyLossInjectionProfile.None);

        var act = () => wrapper.Disconnect(LoopbackTransportHub.HubSentinelId);

        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_owns_inner_when_requested()
    {
        var hub = LoopbackTransportHub.Create();
        var client = hub.Accept("client").Client;
        var wrapper = new LatencyLossWrappingTransport(client, LatencyLossInjectionProfile.None, ownsInner: true);

        wrapper.Dispose();

        var act = () => wrapper.Send(LoopbackTransportHub.HubSentinelId, new byte[] { 0 });
        act.Should().Throw<ObjectDisposedException>();
        hub.Dispose();
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        using var hub = LoopbackTransportHub.Create();
        var client = hub.Accept("client").Client;
        var wrapper = new LatencyLossWrappingTransport(client, LatencyLossInjectionProfile.None);

        wrapper.Dispose();
        wrapper.Dispose();
    }

    [Fact]
    public void Poll_after_dispose_throws()
    {
        using var hub = LoopbackTransportHub.Create();
        var client = hub.Accept("client").Client;
        var wrapper = new LatencyLossWrappingTransport(client, LatencyLossInjectionProfile.None);
        wrapper.Dispose();

        var act = () => wrapper.Poll(new List<NetEvent>());

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Name_passes_through_inner()
    {
        using var hub = LoopbackTransportHub.Create();
        var client = hub.Accept("named-client").Client;
        using var wrapper = new LatencyLossWrappingTransport(client, LatencyLossInjectionProfile.None);

        wrapper.Name.Should().Be(client.Name);
    }

    [Fact]
    public void Inbound_pass_through_forwards_received_events()
    {
        using var hub = LoopbackTransportHub.Create();
        var accepted = hub.Accept("client");
        using var wrapper = new LatencyLossWrappingTransport(accepted.Client, LatencyLossInjectionProfile.None);
        hub.Send(accepted.ServerSidePeerId, new byte[] { 9 });

        var events = new List<NetEvent>();
        wrapper.Poll(events);

        events.Should().Contain(static ev => ev.Kind == NetEventKind.Received);
        wrapper.InboundAttemptCount.Should().Be(1);
        wrapper.InboundDroppedPacketCount.Should().Be(0);
        wrapper.InboundDelayedPacketCount.Should().Be(0);
    }

    [Fact]
    public void Inbound_full_loss_drops_every_received_event()
    {
        using var hub = LoopbackTransportHub.Create();
        var accepted = hub.Accept("client");
        var profile = LatencyLossInjectionProfile.None with { InboundLossRate = 1.0 };
        using var wrapper = new LatencyLossWrappingTransport(accepted.Client, profile);
        for (var i = 0; i < 5; i++)
        {
            hub.Send(accepted.ServerSidePeerId, new byte[] { (byte)i });
        }

        var events = new List<NetEvent>();
        wrapper.Poll(events);

        events.Should().NotContain(static ev => ev.Kind == NetEventKind.Received);
        wrapper.InboundAttemptCount.Should().Be(5);
        wrapper.InboundDroppedPacketCount.Should().Be(5);
    }

    [Fact]
    public void Inbound_latency_queues_until_deadline_then_flushes()
    {
        var time = new TestTimeProvider();
        using var hub = LoopbackTransportHub.Create();
        var accepted = hub.Accept("client");
        var profile = LatencyLossInjectionProfile.None with { InboundAddedLatency = TimeSpan.FromMilliseconds(50) };
        using var wrapper = new LatencyLossWrappingTransport(accepted.Client, profile, time);
        hub.Send(accepted.ServerSidePeerId, new byte[] { 1 });

        var events = new List<NetEvent>();
        wrapper.Poll(events);

        events.Should().NotContain(static ev => ev.Kind == NetEventKind.Received);
        wrapper.PendingScheduledReceiveCount.Should().Be(1);
        wrapper.InboundDelayedPacketCount.Should().Be(1);

        time.Advance(TimeSpan.FromMilliseconds(60));
        wrapper.Poll(events);

        events.Should().Contain(static ev => ev.Kind == NetEventKind.Received);
        wrapper.PendingScheduledReceiveCount.Should().Be(0);
    }

    [Fact]
    public void Inbound_seed_reproduces_drop_pattern_across_runs()
    {
        var profile = LatencyLossInjectionProfile.None with { InboundLossRate = 0.5, InboundSeed = 123 };

        var first = CaptureInboundDropPattern(profile, packetCount: 16);
        var second = CaptureInboundDropPattern(profile, packetCount: 16);

        first.Should().Equal(second);
    }

    [Fact]
    public void Inbound_seed_diverges_between_seeds()
    {
        var seedOne = LatencyLossInjectionProfile.None with { InboundLossRate = 0.5, InboundSeed = 1 };
        var seedTwo = LatencyLossInjectionProfile.None with { InboundLossRate = 0.5, InboundSeed = 2 };

        var first = CaptureInboundDropPattern(seedOne, packetCount: 32);
        var second = CaptureInboundDropPattern(seedTwo, packetCount: 32);

        first.Should().NotEqual(second);
    }

    [Fact]
    public void Inbound_filter_does_not_drop_control_plane_events()
    {
        using var hub = LoopbackTransportHub.Create();
        var accepted = hub.Accept("client");
        var profile = LatencyLossInjectionProfile.None with { InboundLossRate = 1.0 };
        using var wrapper = new LatencyLossWrappingTransport(accepted.Client, profile);
        var events = new List<NetEvent>();

        wrapper.Poll(events);

        events.Should().Contain(static ev => ev.Kind == NetEventKind.Connected);
        wrapper.InboundAttemptCount.Should().Be(0);
        wrapper.InboundDroppedPacketCount.Should().Be(0);
    }

    private static List<bool> CaptureInboundDropPattern(LatencyLossInjectionProfile profile, int packetCount)
    {
        using var hub = LoopbackTransportHub.Create();
        var accepted = hub.Accept("client");
        using var wrapper = new LatencyLossWrappingTransport(accepted.Client, profile);
        var events = new List<NetEvent>();
        wrapper.Poll(events);

        var pattern = new List<bool>(packetCount);
        var previousDrops = 0L;
        for (var i = 0; i < packetCount; i++)
        {
            hub.Send(accepted.ServerSidePeerId, new byte[] { (byte)i });
            wrapper.Poll(events);
            var droppedThisRound = wrapper.InboundDroppedPacketCount > previousDrops;
            pattern.Add(droppedThisRound);
            previousDrops = wrapper.InboundDroppedPacketCount;
        }

        return pattern;
    }

    private static List<bool> CapturePattern(LatencyLossInjectionProfile profile, int packetCount)
    {
        using var hub = LoopbackTransportHub.Create();
        var client = hub.Accept("client").Client;
        using var wrapper = new LatencyLossWrappingTransport(client, profile);
        var pattern = new List<bool>(packetCount);
        var previousDrops = 0L;
        for (var i = 0; i < packetCount; i++)
        {
            wrapper.Send(LoopbackTransportHub.HubSentinelId, new byte[] { (byte)i });
            pattern.Add(wrapper.DroppedPacketCount > previousDrops);
            previousDrops = wrapper.DroppedPacketCount;
        }

        return pattern;
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);

        public void Advance(TimeSpan delta) => _now += delta;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
