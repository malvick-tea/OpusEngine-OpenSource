using System;
using FluentAssertions;
using Opus.Engine.AlphaStress.FramePacing;
using Xunit;

namespace Opus.Engine.AlphaStress.Tests.FramePacing;

public sealed class FramePacingThresholdsTests
{
    [Fact]
    public void Default_validates_cleanly()
    {
        var defaults = FramePacingThresholds.Default;

        var act = defaults.Validate;

        act.Should().NotThrow();
    }

    [Fact]
    public void Default_p95_matches_alpha_frame_budget()
    {
        var defaults = FramePacingThresholds.Default;

        defaults.P95Limit.Should().Be(TimeSpan.FromMilliseconds(33.4));
    }

    [Fact]
    public void Default_hitch_count_limit_is_one()
    {
        var defaults = FramePacingThresholds.Default;

        defaults.HitchCountLimit.Should().Be(1);
    }

    [Fact]
    public void Validate_rejects_non_positive_p95()
    {
        var thresholds = FramePacingThresholds.Default with { P95Limit = TimeSpan.Zero };

        var act = thresholds.Validate;

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("P95Limit");
    }

    [Fact]
    public void Validate_rejects_p99_below_p95()
    {
        var thresholds = FramePacingThresholds.Default with
        {
            P95Limit = TimeSpan.FromMilliseconds(40),
            P99Limit = TimeSpan.FromMilliseconds(30),
        };

        var act = thresholds.Validate;

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("P99Limit");
    }

    [Fact]
    public void Validate_rejects_max_below_p99()
    {
        var thresholds = FramePacingThresholds.Default with
        {
            P99Limit = TimeSpan.FromMilliseconds(60),
            MaxLimit = TimeSpan.FromMilliseconds(50),
        };

        var act = thresholds.Validate;

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("MaxLimit");
    }

    [Fact]
    public void Validate_rejects_non_positive_hitch_threshold()
    {
        var thresholds = FramePacingThresholds.Default with { HitchThreshold = TimeSpan.Zero };

        var act = thresholds.Validate;

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("HitchThreshold");
    }

    [Fact]
    public void Validate_rejects_negative_hitch_count_limit()
    {
        var thresholds = FramePacingThresholds.Default with { HitchCountLimit = -1 };

        var act = thresholds.Validate;

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("HitchCountLimit");
    }
}
