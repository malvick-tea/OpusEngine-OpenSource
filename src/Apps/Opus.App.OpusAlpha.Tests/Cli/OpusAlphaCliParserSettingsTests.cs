using System;
using FluentAssertions;
using Opus.App.OpusAlpha.Cli;
using Xunit;

namespace Opus.App.OpusAlpha.Tests.Cli;

public sealed class OpusAlphaCliParserSettingsTests
{
    [Fact]
    public void Settings_option_parses_the_path()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "--settings", "tester.json" });

        args.Mode.Should().Be(OpusAlphaMode.Window);
        args.SettingsPath.Should().Be("tester.json");
    }

    [Fact]
    public void Window_mode_defaults_to_no_settings_path()
    {
        var args = OpusAlphaCliParser.Parse(Array.Empty<string>());

        args.SettingsPath.Should().BeNull();
    }

    [Fact]
    public void Positional_asset_and_settings_option_coexist()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "tank.glb", "--settings", "tester.json" });

        args.AssetPath.Should().Be("tank.glb");
        args.SettingsPath.Should().Be("tester.json");
    }

    [Fact]
    public void Missing_settings_value_routes_to_help()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "--settings" });

        args.Mode.Should().Be(OpusAlphaMode.Help);
        args.HelpReason.Should().Contain("--settings");
    }
}
