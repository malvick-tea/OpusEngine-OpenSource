using System;
using FluentAssertions;
using Opus.Engine.AlphaHarness.Smoke;
using Opus.Engine.AlphaStress.FramePacing;
using Opus.Engine.AlphaStress.Memory;
using Opus.Engine.AlphaStress.Stress;
using Xunit;

namespace Opus.Engine.AlphaStress.Tests.Stress;

public sealed class AlphaStressProfileTests
{
    [Fact]
    public void Default_validates_cleanly()
    {
        var profile = AlphaStressProfile.Default;

        var act = profile.Validate;

        act.Should().NotThrow();
    }

    [Fact]
    public void Default_uses_documented_iteration_count_constant()
    {
        var profile = AlphaStressProfile.Default;

        profile.IterationCount.Should().Be(AlphaStressProfile.DefaultIterationCount);
        profile.IterationCount.Should().Be(5);
    }

    [Fact]
    public void Default_uses_smoke_profile_default_as_iteration_profile()
    {
        var profile = AlphaStressProfile.Default;

        profile.IterationProfile.FrameTarget.Should().Be(AlphaSmokeProfile.Default.FrameTarget);
        profile.IterationProfile.SmokeName.Should().Be("opus-alpha-stress-iter");
    }

    [Fact]
    public void Validate_rejects_null_iteration_profile()
    {
        var profile = AlphaStressProfile.Default with { IterationProfile = null! };

        var act = profile.Validate;

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Validate_rejects_null_frame_pacing()
    {
        var profile = AlphaStressProfile.Default with { FramePacing = null! };

        var act = profile.Validate;

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Validate_rejects_null_memory()
    {
        var profile = AlphaStressProfile.Default with { Memory = null! };

        var act = profile.Validate;

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(AlphaStressProfile.MaximumIterationCount + 1)]
    public void Validate_rejects_iteration_count_outside_range(int iterations)
    {
        var profile = AlphaStressProfile.Default with { IterationCount = iterations };

        var act = profile.Validate;

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("IterationCount");
    }

    [Fact]
    public void Validate_rejects_non_positive_wall_clock_budget()
    {
        var profile = AlphaStressProfile.Default with { WallClockBudget = TimeSpan.Zero };

        var act = profile.Validate;

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("WallClockBudget");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_rejects_empty_stress_name(string name)
    {
        var profile = AlphaStressProfile.Default with { StressName = name };

        var act = profile.Validate;

        act.Should().Throw<ArgumentException>().WithParameterName("StressName");
    }
}
