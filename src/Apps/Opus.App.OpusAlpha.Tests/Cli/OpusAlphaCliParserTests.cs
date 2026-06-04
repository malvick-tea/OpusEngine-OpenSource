using FluentAssertions;
using Opus.App.OpusAlpha.Cli;
using Opus.Engine.AlphaHarness.Scenes;
using Xunit;

namespace Opus.App.OpusAlpha.Tests.Cli;

public sealed class OpusAlphaCliParserTests
{
    [Fact]
    public void Empty_arguments_select_window_mode_with_defaults()
    {
        var args = OpusAlphaCliParser.Parse(System.Array.Empty<string>());

        args.Mode.Should().Be(OpusAlphaMode.Window);
        args.SceneScale.Should().Be(AlphaSceneScale.Small);
        args.AssetPath.Should().BeNull();
        args.HelpReason.Should().BeEmpty();
    }

    [Fact]
    public void Positional_token_in_window_mode_becomes_asset_path()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "C:/assets/sample.glb" });

        args.Mode.Should().Be(OpusAlphaMode.Window);
        args.AssetPath.Should().Be("C:/assets/sample.glb");
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Help_flag_selects_help_mode(string flag)
    {
        var args = OpusAlphaCliParser.Parse(new[] { flag });

        args.Mode.Should().Be(OpusAlphaMode.Help);
        args.HelpReason.Should().BeEmpty();
    }

    [Theory]
    [InlineData("smoke", OpusAlphaMode.Smoke)]
    [InlineData("validate-package", OpusAlphaMode.ValidatePackage)]
    [InlineData("check-machine", OpusAlphaMode.CheckMachine)]
    [InlineData("soak", OpusAlphaMode.Soak)]
    [InlineData("stress", OpusAlphaMode.Stress)]
    public void Recognised_subcommand_dispatches_to_mode(string token, OpusAlphaMode expected)
    {
        var args = OpusAlphaCliParser.Parse(new[] { token });

        args.Mode.Should().Be(expected);
    }

    [Fact]
    public void Scene_option_parses_large()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "--scene", "large" });

        args.SceneScale.Should().Be(AlphaSceneScale.Large);
        args.Mode.Should().Be(OpusAlphaMode.Window);
    }

    [Fact]
    public void Unsupported_scene_value_routes_to_help_with_reason()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "--scene", "huge" });

        args.Mode.Should().Be(OpusAlphaMode.Help);
        args.HelpReason.Should().Contain("--scene");
    }

    [Fact]
    public void Smoke_options_round_trip()
    {
        var args = OpusAlphaCliParser.Parse(new[]
        {
            "smoke", "--frames", "120", "--screenshot-frame", "42", "--report", "C:/out",
        });

        args.Mode.Should().Be(OpusAlphaMode.Smoke);
        args.SmokeFrameCount.Should().Be(120);
        args.SmokeScreenshotFrame.Should().Be(42);
        args.SmokeReportPath.Should().Be("C:/out");
    }

    [Fact]
    public void Validate_package_carries_package_path()
    {
        var args = OpusAlphaCliParser.Parse(new[]
        {
            "validate-package", "--package", "C:/pkg",
        });

        args.Mode.Should().Be(OpusAlphaMode.ValidatePackage);
        args.PackagePath.Should().Be("C:/pkg");
    }

    [Fact]
    public void Check_machine_carries_reference_path()
    {
        var args = OpusAlphaCliParser.Parse(new[]
        {
            "check-machine", "--reference", "C:/profiles/windows.json",
        });

        args.Mode.Should().Be(OpusAlphaMode.CheckMachine);
        args.MachineReferencePath.Should().Be("C:/profiles/windows.json");
    }

    [Fact]
    public void Soak_options_round_trip()
    {
        var args = OpusAlphaCliParser.Parse(new[]
        {
            "soak", "--peers", "8", "--packets", "32", "--payload", "512",
        });

        args.Mode.Should().Be(OpusAlphaMode.Soak);
        args.SoakPeers.Should().Be(8);
        args.SoakPacketsPerPeer.Should().Be(32);
        args.SoakPayloadBytes.Should().Be(512);
    }

    [Theory]
    [InlineData("--frames")]
    [InlineData("--peers")]
    public void Missing_value_for_option_routes_to_help(string option)
    {
        var args = OpusAlphaCliParser.Parse(new[] { "smoke", option });

        args.Mode.Should().Be(OpusAlphaMode.Help);
        args.HelpReason.Should().Contain(option);
    }

    [Fact]
    public void Option_followed_by_another_option_is_missing_value()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "smoke", "--frames", "--screenshot-frame", "1" });

        args.Mode.Should().Be(OpusAlphaMode.Help);
        args.HelpReason.Should().Contain("--frames");
    }

    [Theory]
    [InlineData("--frames", "abc")]
    [InlineData("--peers", "-3")]
    public void Out_of_range_or_unparseable_integer_routes_to_help(string option, string value)
    {
        var args = OpusAlphaCliParser.Parse(new[] { "smoke", option, value });

        args.Mode.Should().Be(OpusAlphaMode.Help);
        args.HelpReason.Should().Contain(option);
    }

    [Fact]
    public void Unknown_option_routes_to_help()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "smoke", "--unknown", "1" });

        args.Mode.Should().Be(OpusAlphaMode.Help);
        args.HelpReason.Should().Contain("--unknown");
    }

    [Fact]
    public void Diagnostics_directory_override_is_applied()
    {
        var args = OpusAlphaCliParser.Parse(new[]
        {
            "smoke", "--diagnostics-dir", "C:/diag",
        });

        args.DiagnosticsDirectory.Should().Be("C:/diag");
    }

    [Fact]
    public void Asset_via_explicit_option_overrides_positional()
    {
        var args = OpusAlphaCliParser.Parse(new[]
        {
            "--asset", "C:/explicit.glb",
        });

        args.AssetPath.Should().Be("C:/explicit.glb");
    }
}
