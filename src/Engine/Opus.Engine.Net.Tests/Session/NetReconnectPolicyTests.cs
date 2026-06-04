using System;
using FluentAssertions;
using Opus.Engine.Net.Session;
using Xunit;

namespace Opus.Engine.Net.Tests.Session;

/// <summary>Pure data validation for <see cref="NetReconnectPolicy"/> and
/// <see cref="NetReconnectSchedule"/>. Pins the rejection of bad inputs against the
/// engine's boundary discipline.</summary>
public sealed class NetReconnectPolicyTests
{
    [Fact]
    public void Validate_rejects_negative_MaxAttempts()
    {
        var policy = new NetReconnectPolicy(-1, TimeSpan.Zero, TimeSpan.Zero, 1.0);
        Action act = policy.Validate;
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Validate_rejects_negative_BaseDelay()
    {
        var policy = new NetReconnectPolicy(1, TimeSpan.FromMilliseconds(-1), TimeSpan.Zero, 1.0);
        Action act = policy.Validate;
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Validate_rejects_MaxDelay_smaller_than_BaseDelay()
    {
        var policy = new NetReconnectPolicy(1, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(100), 1.0);
        Action act = policy.Validate;
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Validate_rejects_subunity_BackoffMultiplier()
    {
        var policy = new NetReconnectPolicy(1, TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(20), 0.5);
        Action act = policy.Validate;
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Validate_rejects_NaN_BackoffMultiplier()
    {
        var policy = new NetReconnectPolicy(1, TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(20), double.NaN);
        Action act = policy.Validate;
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void HasBudget_falls_to_false_when_attempts_match_max()
    {
        var policy = new NetReconnectPolicy(2, TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1), 1.0);
        NetReconnectSchedule.HasBudget(policy, 0).Should().BeTrue();
        NetReconnectSchedule.HasBudget(policy, 1).Should().BeTrue();
        NetReconnectSchedule.HasBudget(policy, 2).Should().BeFalse();
    }

    [Fact]
    public void ComputeDelay_returns_zero_when_BaseDelay_is_zero()
    {
        var policy = new NetReconnectPolicy(3, TimeSpan.Zero, TimeSpan.Zero, 1.0);
        NetReconnectSchedule.ComputeDelay(policy, 1).Should().Be(TimeSpan.Zero);
        NetReconnectSchedule.ComputeDelay(policy, 3).Should().Be(TimeSpan.Zero);
    }
}
