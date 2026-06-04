using System;
using FluentAssertions;
using Opus.App.OpusAlpha.Cli;
using Xunit;

namespace Opus.App.OpusAlpha.Tests.Cli;

public sealed class OpusAlphaCliParserFaultInjectionTests
{
    [Theory]
    [InlineData("startup", AlphaFaultKind.Startup)]
    [InlineData("content", AlphaFaultKind.Content)]
    [InlineData("device-lost", AlphaFaultKind.DeviceLost)]
    public void Inject_failure_option_parses_known_kind(string token, AlphaFaultKind expected)
    {
        var args = OpusAlphaCliParser.Parse(new[] { "--inject-failure", token });

        args.Mode.Should().Be(OpusAlphaMode.Window);
        args.InjectFailure.Should().Be(expected);
    }

    [Fact]
    public void Inject_failure_value_is_case_insensitive()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "--inject-failure", "Device-Lost" });

        args.InjectFailure.Should().Be(AlphaFaultKind.DeviceLost);
    }

    [Fact]
    public void Window_mode_defaults_to_no_injection()
    {
        var args = OpusAlphaCliParser.Parse(Array.Empty<string>());

        args.InjectFailure.Should().Be(AlphaFaultKind.None);
    }

    [Fact]
    public void Unsupported_inject_failure_value_routes_to_help_with_reason()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "--inject-failure", "meltdown" });

        args.Mode.Should().Be(OpusAlphaMode.Help);
        args.HelpReason.Should().Contain("--inject-failure");
    }

    [Fact]
    public void Missing_inject_failure_value_routes_to_help()
    {
        var args = OpusAlphaCliParser.Parse(new[] { "--inject-failure" });

        args.Mode.Should().Be(OpusAlphaMode.Help);
        args.HelpReason.Should().Contain("--inject-failure");
    }
}
