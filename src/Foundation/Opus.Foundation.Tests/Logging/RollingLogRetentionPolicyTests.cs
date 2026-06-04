using System;
using FluentAssertions;
using Opus.Foundation;
using Xunit;

namespace Opus.Foundation.Tests.Logging;

public sealed class RollingLogRetentionPolicyTests
{
    [Fact]
    public void Default_policy_is_active_with_documented_constants()
    {
        var policy = RollingLogRetentionPolicy.Default;

        policy.IsActive.Should().BeTrue();
        policy.MaxFileCount.Should().Be(RollingLogRetentionPolicy.DefaultMaxFileCount);
        policy.MaxAge.Should().Be(RollingLogRetentionPolicy.DefaultMaxAge);
    }

    [Fact]
    public void Disabled_policy_is_inactive_and_validates()
    {
        var policy = RollingLogRetentionPolicy.Disabled;

        policy.IsActive.Should().BeFalse();
        policy.Invoking(p => p.Validate()).Should().NotThrow();
    }

    [Fact]
    public void Negative_max_file_count_is_rejected()
    {
        var policy = new RollingLogRetentionPolicy(MaxFileCount: -1, MaxAge: TimeSpan.Zero);

        policy.Invoking(p => p.Validate()).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Negative_max_age_is_rejected()
    {
        var policy = new RollingLogRetentionPolicy(MaxFileCount: 0, MaxAge: TimeSpan.FromSeconds(-1));

        policy.Invoking(p => p.Validate()).Should().Throw<ArgumentOutOfRangeException>();
    }
}
