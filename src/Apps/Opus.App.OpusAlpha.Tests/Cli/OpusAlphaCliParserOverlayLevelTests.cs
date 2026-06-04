using System;
using FluentAssertions;
using Opus.App.OpusAlpha.Cli;
using Opus.Engine.Diagnostics.Overlay;
using Xunit;

namespace Opus.App.OpusAlpha.Tests.Cli;

public sealed class OpusAlphaCliParserOverlayLevelTests
{
    [Theory]
    [InlineData("off", DiagnosticOverlayLevel.Off)]
    [InlineData("minimal", DiagnosticOverlayLevel.Minimal)]
    [InlineData("full", DiagnosticOverlayLevel.Full)]
    public void Overlay_level_option_parses_known_value(string token, DiagnosticOverlayLevel expected)
    {
        var args = OpusAlphaCliParser.Parse(new[] { "--overlay-level", token });

        args.Mode.Should().Be(OpusAlphaMode.Window);
        args.OverlayLevel.Should().Be(expected);
    }

    [Fact]
    public void Overlay_level_value_is_case_insensitive()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "--overlay-level", "OFF" });

        args.OverlayLevel.Should().Be(DiagnosticOverlayLevel.Off);
    }

    [Fact]
    public void Window_mode_defaults_to_no_overlay_level_override()
    {
        var args = OpusAlphaCliParser.Parse(Array.Empty<string>());

        args.OverlayLevel.Should().BeNull();
    }

    [Fact]
    public void Unsupported_overlay_level_routes_to_help_with_reason()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "--overlay-level", "verbose" });

        args.Mode.Should().Be(OpusAlphaMode.Help);
        args.HelpReason.Should().Contain("--overlay-level");
    }

    [Fact]
    public void Missing_overlay_level_value_routes_to_help()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "--overlay-level" });

        args.Mode.Should().Be(OpusAlphaMode.Help);
        args.HelpReason.Should().Contain("--overlay-level");
    }

    [Fact]
    public void Known_issues_overlay_option_is_distinct_from_the_window_overlay_level()
    {
        var args = OpusAlphaCliParser.Parse(new[]
        {
            "known-issues-merge", "--base", "base.json", "--overlay", "overlay.json", "--output", "merged.json",
        });

        args.Mode.Should().Be(OpusAlphaMode.KnownIssuesMerge);
        args.KnownIssuesOverlayPath.Should().Be("overlay.json");
        args.OverlayLevel.Should().BeNull();
    }
}
