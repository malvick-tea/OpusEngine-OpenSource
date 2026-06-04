using System;
using FluentAssertions;
using Opus.App.OpusAlpha.Cli;
using Opus.Engine.AlphaHarness.Scenes;
using Xunit;

namespace Opus.App.OpusAlpha.Tests.Cli;

public sealed class OpusAlphaCliParserFrameBudgetTests
{
    [Fact]
    public void Frame_budget_flag_enables_the_watchdog_in_window_mode()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "--frame-budget" });

        args.Mode.Should().Be(OpusAlphaMode.Window);
        args.EnableFrameBudget.Should().BeTrue();
    }

    [Fact]
    public void Window_mode_defaults_to_disabled_frame_budget()
    {
        var args = OpusAlphaCliParser.Parse(Array.Empty<string>());

        args.EnableFrameBudget.Should().BeFalse();
    }

    [Fact]
    public void Frame_budget_flag_does_not_consume_the_following_option_value()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "--frame-budget", "--scene", "large" });

        args.EnableFrameBudget.Should().BeTrue();
        args.SceneScale.Should().Be(AlphaSceneScale.Large);
        args.Mode.Should().Be(OpusAlphaMode.Window);
    }

    [Fact]
    public void Frame_budget_flag_combines_with_a_positional_asset_path()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "tank.glb", "--frame-budget" });

        args.AssetPath.Should().Be("tank.glb");
        args.EnableFrameBudget.Should().BeTrue();
    }
}
