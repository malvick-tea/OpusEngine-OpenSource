using System;
using FluentAssertions;
using Opus.Engine.Diagnostics.Overlay;
using Xunit;

namespace Opus.Engine.Diagnostics.Tests.Overlay;

public sealed class DiagnosticOverlayOptionsTests
{
    [Fact]
    public void Default_options_are_enabled_full_overlay()
    {
        var options = DiagnosticOverlayOptions.Default;

        options.Enabled.Should().BeTrue();
        options.Level.Should().Be(DiagnosticOverlayLevel.Full);
        options.ShouldDraw.Should().BeTrue();
        options.RefreshInterval.Should().Be(DiagnosticOverlayOptions.DefaultRefreshInterval);
        options.ToggleKey.Should().Be(DiagnosticOverlayToggleKey.F10);
    }

    [Fact]
    public void Off_level_does_not_draw()
    {
        var options = DiagnosticOverlayOptions.Default with { Level = DiagnosticOverlayLevel.Off };

        options.ShouldDraw.Should().BeFalse();
    }

    [Fact]
    public void Invalid_refresh_interval_is_rejected()
    {
        var options = DiagnosticOverlayOptions.Default with { RefreshInterval = TimeSpan.Zero };

        var act = () => options.Validate();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65)]
    public void Invalid_row_caps_are_rejected(int maxRows)
    {
        var options = DiagnosticOverlayOptions.Default with { MaxRows = maxRows };

        var act = () => options.Validate();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
