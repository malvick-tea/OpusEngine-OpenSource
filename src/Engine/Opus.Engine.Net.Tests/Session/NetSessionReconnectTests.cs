using System;
using System.Collections.Generic;
using FluentAssertions;
using Opus.Engine.Net.Session;
using Opus.Engine.Net.Tests.Support;
using Xunit;

namespace Opus.Engine.Net.Tests.Session;

/// <summary>Reconnect cadence behavior pinned through <see cref="NetSession"/>'s state
/// machine. The transport factory recycles loopback pairs so the reconnect path is
/// exercised end to end without a real socket.</summary>
public sealed class NetSessionReconnectTests
{
    [Fact]
    public void ComputeDelay_grows_with_backoff_multiplier_up_to_cap()
    {
        var policy = new NetReconnectPolicy(
            MaxAttempts: 4,
            BaseDelay: TimeSpan.FromMilliseconds(100),
            MaxDelay: TimeSpan.FromMilliseconds(500),
            BackoffMultiplier: 2.0);

        NetReconnectSchedule.ComputeDelay(policy, 1).TotalMilliseconds.Should().Be(100);
        NetReconnectSchedule.ComputeDelay(policy, 2).TotalMilliseconds.Should().Be(200);
        NetReconnectSchedule.ComputeDelay(policy, 3).TotalMilliseconds.Should().Be(400);
        NetReconnectSchedule.ComputeDelay(policy, 4).TotalMilliseconds.Should().Be(500);
    }

    [Fact]
    public void Disabled_policy_exhausts_immediately_after_first_drop()
    {
        using var factory = new LoopbackClientTransportFactory();
        using var session = NetSession.Client(
            new NetSessionOptions(NetSessionRole.Client, "client-no-retry", NetReconnectPolicy.Disabled),
            factory);
        var events = new List<NetSessionEvent>();
        session.Tick(TimeSpan.Zero, events.Add);
        var link = factory.LastLink!;

        link.Server.Disconnect(link.ClientPeerId);
        session.Tick(TimeSpan.Zero, events.Add);

        session.State.Should().Be(NetSessionState.Faulted);
        events.Should().Contain(e => e.Kind == NetSessionEventKind.ReconnectExhausted
            && e.DiagnosticCode == NetDiagnosticCodes.SessionReconnectExhausted);
        session.LastFault.Should().NotBeNull();
        session.LastFault!.Code.Should().Be(NetSessionFaultCode.ReconnectBudgetExhausted);
    }

    [Fact]
    public void Active_policy_schedules_and_executes_a_reconnect_attempt()
    {
        using var factory = new LoopbackClientTransportFactory();
        var policy = new NetReconnectPolicy(
            MaxAttempts: 3,
            BaseDelay: TimeSpan.FromMilliseconds(10),
            MaxDelay: TimeSpan.FromMilliseconds(20),
            BackoffMultiplier: 1.0);
        using var session = NetSession.Client(
            new NetSessionOptions(NetSessionRole.Client, "client-retry", policy),
            factory);
        var events = new List<NetSessionEvent>();
        session.Tick(TimeSpan.Zero, events.Add);
        var link = factory.LastLink!;

        link.Server.Disconnect(link.ClientPeerId);
        session.Tick(TimeSpan.Zero, events.Add);
        events.Should().Contain(e => e.Kind == NetSessionEventKind.ReconnectScheduled);
        session.State.Should().Be(NetSessionState.Reconnecting);

        session.Tick(TimeSpan.FromMilliseconds(50), events.Add);
        events.Should().Contain(e => e.Kind == NetSessionEventKind.ReconnectAttempted);
        session.Statistics.ReconnectAttempts.Should().Be(1);
        factory.CreateCallCount.Should().BeGreaterThan(1);
    }

    [Fact]
    public void Factory_throwing_records_TransportFault()
    {
        using var factory = new LoopbackClientTransportFactory { ThrowOnCreate = true };
        using var session = NetSession.Client(
            new NetSessionOptions(NetSessionRole.Client, "client-faulty", NetReconnectPolicy.Disabled),
            factory);
        var events = new List<NetSessionEvent>();

        session.Tick(TimeSpan.Zero, events.Add);

        session.State.Should().Be(NetSessionState.Faulted);
        session.LastFault.Should().NotBeNull();
        session.LastFault!.Code.Should().Be(NetSessionFaultCode.ReconnectFactoryThrew);
        events.Should().Contain(e => e.Kind == NetSessionEventKind.TransportFault
            && e.DiagnosticCode == NetDiagnosticCodes.SessionTransportFault);
    }

    [Fact]
    public void Successful_reconnect_resets_attempt_budget_so_a_later_drop_can_retry_again()
    {
        // Reset-on-Connected is the chosen alpha policy: a sustained reconnect is a
        // recovery, not a permanent burn against the budget. The test pins that policy
        // so a flapping tester laptop keeps reconnecting under MaxAttempts=1 instead
        // of dying after the first two drops.
        using var factory = new LoopbackClientTransportFactory();
        var policy = new NetReconnectPolicy(
            MaxAttempts: 1,
            BaseDelay: TimeSpan.FromMilliseconds(1),
            MaxDelay: TimeSpan.FromMilliseconds(1),
            BackoffMultiplier: 1.0);
        using var session = NetSession.Client(
            new NetSessionOptions(NetSessionRole.Client, "client-1", policy),
            factory);
        var events = new List<NetSessionEvent>();
        session.Tick(TimeSpan.Zero, events.Add);
        var firstLink = factory.LastLink!;

        firstLink.Server.Disconnect(firstLink.ClientPeerId);
        session.Tick(TimeSpan.Zero, events.Add);
        session.Tick(TimeSpan.FromMilliseconds(10), events.Add);

        var secondLink = factory.LastLink!;
        secondLink.Server.Disconnect(secondLink.ClientPeerId);
        session.Tick(TimeSpan.Zero, events.Add);

        session.State.Should().Be(NetSessionState.Reconnecting);
        session.Statistics.ReconnectAttempts.Should().Be(1);
    }

    [Fact]
    public void Repeated_failures_without_recovery_exhaust_the_budget()
    {
        // Counter-policy to the reset-on-Connected behaviour: when reconnect attempts
        // keep failing (factory throws between successes), the budget is consumed.
        using var factory = new LoopbackClientTransportFactory();
        var policy = new NetReconnectPolicy(
            MaxAttempts: 1,
            BaseDelay: TimeSpan.FromMilliseconds(1),
            MaxDelay: TimeSpan.FromMilliseconds(1),
            BackoffMultiplier: 1.0);
        using var session = NetSession.Client(
            new NetSessionOptions(NetSessionRole.Client, "client-1", policy),
            factory);
        var events = new List<NetSessionEvent>();
        session.Tick(TimeSpan.Zero, events.Add);
        var firstLink = factory.LastLink!;

        firstLink.Server.Disconnect(firstLink.ClientPeerId);
        session.Tick(TimeSpan.Zero, events.Add);
        factory.ThrowOnCreate = true;
        session.Tick(TimeSpan.FromMilliseconds(10), events.Add);

        session.State.Should().Be(NetSessionState.Faulted);
        session.LastFault!.Code.Should().Be(NetSessionFaultCode.ReconnectFactoryThrew);
    }
}
