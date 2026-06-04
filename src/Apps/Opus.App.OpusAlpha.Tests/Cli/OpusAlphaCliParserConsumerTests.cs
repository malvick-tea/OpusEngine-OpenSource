using System;
using FluentAssertions;
using Opus.App.OpusAlpha.Cli;
using Xunit;

namespace Opus.App.OpusAlpha.Tests.Cli;

public sealed class OpusAlphaCliParserConsumerTests
{
    [Fact]
    public void Consumer_option_parses_the_assembly_path()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "--consumer", "plugins/consumer.dll" });

        args.Mode.Should().Be(OpusAlphaMode.Window);
        args.ConsumerAssemblyPath.Should().Be("plugins/consumer.dll");
    }

    [Fact]
    public void Window_mode_defaults_to_no_consumer_assembly()
    {
        var args = OpusAlphaCliParser.Parse(Array.Empty<string>());

        args.ConsumerAssemblyPath.Should().BeNull();
    }

    [Fact]
    public void Positional_asset_and_consumer_option_coexist()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "tank.glb", "--consumer", "consumer.dll" });

        args.Mode.Should().Be(OpusAlphaMode.Window);
        args.AssetPath.Should().Be("tank.glb");
        args.ConsumerAssemblyPath.Should().Be("consumer.dll");
    }

    [Fact]
    public void Missing_consumer_value_routes_to_help()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "--consumer" });

        args.Mode.Should().Be(OpusAlphaMode.Help);
        args.HelpReason.Should().Contain("--consumer");
    }
}
