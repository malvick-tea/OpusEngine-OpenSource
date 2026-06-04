using System;
using FluentAssertions;
using Opus.Engine.AlphaHarness.Smoke;
using Xunit;

namespace Opus.Engine.AlphaHarness.Tests.Smoke;

public sealed class AlphaSmokeProfileTests
{
    [Fact]
    public void Default_profile_validates_cleanly()
    {
        var profile = AlphaSmokeProfile.Default;

        profile.Invoking(p => p.Validate()).Should().NotThrow();
        profile.FrameTarget.Should().Be(AlphaSmokeProfile.DefaultFrameTarget);
        profile.CapturesScreenshot.Should().BeFalse();
        profile.SmokeName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void With_screenshot_sets_the_frame_index_and_enables_capture()
    {
        var profile = AlphaSmokeProfile.Default.WithScreenshot(frameIndex: 12);

        profile.CapturesScreenshot.Should().BeTrue();
        profile.ScreenshotFrameIndex.Should().Be(12);
        profile.Validate();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(AlphaSmokeProfile.MaximumFrameTarget + 1)]
    public void Frame_target_outside_bounds_is_rejected(int frameTarget)
    {
        var profile = AlphaSmokeProfile.Default with { FrameTarget = frameTarget };

        profile.Invoking(p => p.Validate())
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*FrameTarget*");
    }

    [Fact]
    public void Zero_frame_delta_is_rejected()
    {
        var profile = AlphaSmokeProfile.Default with { FrameDelta = TimeSpan.Zero };

        profile.Invoking(p => p.Validate())
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*FrameDelta*");
    }

    [Fact]
    public void Zero_wallclock_budget_is_rejected()
    {
        var profile = AlphaSmokeProfile.Default with { WallClockBudget = TimeSpan.Zero };

        profile.Invoking(p => p.Validate())
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*WallClockBudget*");
    }

    [Fact]
    public void Screenshot_frame_index_outside_target_range_is_rejected()
    {
        var profile = AlphaSmokeProfile.Default.WithScreenshot(frameIndex: AlphaSmokeProfile.DefaultFrameTarget);

        profile.Invoking(p => p.Validate())
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*ScreenshotFrameIndex*");
    }

    [Fact]
    public void Empty_smoke_name_is_rejected()
    {
        var profile = AlphaSmokeProfile.Default with { SmokeName = "  " };

        profile.Invoking(p => p.Validate())
            .Should().Throw<ArgumentException>()
            .WithMessage("*SmokeName*");
    }

    [Fact]
    public void Screenshot_disabled_default_skips_frame_index_validation()
    {
        var profile = AlphaSmokeProfile.Default with
        {
            CapturesScreenshot = false,
            ScreenshotFrameIndex = AlphaSmokeProfile.DefaultFrameTarget * 4,
        };

        profile.Invoking(p => p.Validate()).Should().NotThrow();
    }
}
