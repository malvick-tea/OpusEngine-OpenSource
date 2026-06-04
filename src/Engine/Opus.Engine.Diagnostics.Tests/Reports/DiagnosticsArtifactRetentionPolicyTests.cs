using System;
using FluentAssertions;
using Opus.Engine.Diagnostics.Reports;
using Xunit;

namespace Opus.Engine.Diagnostics.Tests.Reports;

public sealed class DiagnosticsArtifactRetentionPolicyTests
{
    [Fact]
    public void Default_policy_is_active_with_documented_constants()
    {
        var policy = DiagnosticsArtifactRetentionPolicy.Default;

        policy.IsActive.Should().BeTrue();
        policy.MaxPairCount.Should().Be(DiagnosticsArtifactRetentionPolicy.DefaultMaxPairCount);
        policy.MaxAge.Should().Be(DiagnosticsArtifactRetentionPolicy.DefaultMaxAge);
    }

    [Fact]
    public void Disabled_policy_is_inactive_and_validates()
    {
        var policy = DiagnosticsArtifactRetentionPolicy.Disabled;

        policy.IsActive.Should().BeFalse();
        policy.Invoking(p => p.Validate()).Should().NotThrow();
    }

    [Fact]
    public void Negative_max_pair_count_is_rejected()
    {
        var policy = new DiagnosticsArtifactRetentionPolicy(MaxPairCount: -1, MaxAge: TimeSpan.Zero);

        policy.Invoking(p => p.Validate()).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Negative_max_age_is_rejected()
    {
        var policy = new DiagnosticsArtifactRetentionPolicy(MaxPairCount: 0, MaxAge: TimeSpan.FromMilliseconds(-1));

        policy.Invoking(p => p.Validate()).Should().Throw<ArgumentOutOfRangeException>();
    }
}
