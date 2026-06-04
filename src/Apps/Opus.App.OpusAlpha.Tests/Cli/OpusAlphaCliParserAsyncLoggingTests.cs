using System;
using FluentAssertions;
using Opus.App.OpusAlpha.Cli;
using Opus.Engine.AlphaHarness.Scenes;
using Xunit;

namespace Opus.App.OpusAlpha.Tests.Cli;

public sealed class OpusAlphaCliParserAsyncLoggingTests
{
    [Fact]
    public void Async_logging_flag_enables_off_thread_writes_in_window_mode()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "--async-logging" });

        args.Mode.Should().Be(OpusAlphaMode.Window);
        args.EnableAsyncLogging.Should().BeTrue();
    }

    [Fact]
    public void Window_mode_defaults_to_synchronous_logging()
    {
        var args = OpusAlphaCliParser.Parse(Array.Empty<string>());

        args.EnableAsyncLogging.Should().BeFalse();
    }

    [Fact]
    public void Async_logging_flag_does_not_consume_the_following_option_value()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "--async-logging", "--scene", "large" });

        args.EnableAsyncLogging.Should().BeTrue();
        args.SceneScale.Should().Be(AlphaSceneScale.Large);
        args.Mode.Should().Be(OpusAlphaMode.Window);
    }

    [Fact]
    public void Async_logging_flag_combines_with_the_frame_budget_flag()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "--frame-budget", "--async-logging" });

        args.EnableFrameBudget.Should().BeTrue();
        args.EnableAsyncLogging.Should().BeTrue();
    }

    [Fact]
    public void Async_logging_flag_combines_with_a_positional_asset_path()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "tank.glb", "--async-logging" });

        args.AssetPath.Should().Be("tank.glb");
        args.EnableAsyncLogging.Should().BeTrue();
    }
}
