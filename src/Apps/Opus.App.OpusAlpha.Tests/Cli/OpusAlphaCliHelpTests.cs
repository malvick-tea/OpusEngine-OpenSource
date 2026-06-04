using System.IO;
using FluentAssertions;
using Opus.App.OpusAlpha.Cli;
using Xunit;

namespace Opus.App.OpusAlpha.Tests.Cli;

public sealed class OpusAlphaCliHelpTests
{
    [Fact]
    public void Render_includes_usage_lines_for_every_mode()
    {
        var banner = OpusAlphaCliHelp.Render(reason: string.Empty);

        banner.Should().Contain("opus-alpha-host smoke");
        banner.Should().Contain("opus-alpha-host validate-package");
        banner.Should().Contain("opus-alpha-host check-machine");
        banner.Should().Contain("opus-alpha-host soak");
        banner.Should().Contain("opus-alpha-host stress");
        banner.Should().Contain("--help");
    }

    [Fact]
    public void Render_documents_window_async_logging_option()
    {
        var banner = OpusAlphaCliHelp.Render(reason: string.Empty);

        banner.Should().Contain("--async-logging");
    }

    [Fact]
    public void Render_documents_window_consumer_option()
    {
        var banner = OpusAlphaCliHelp.Render(reason: string.Empty);

        banner.Should().Contain("--consumer");
    }

    [Fact]
    public void Render_documents_window_settings_option()
    {
        var banner = OpusAlphaCliHelp.Render(reason: string.Empty);

        banner.Should().Contain("--settings");
    }

    [Fact]
    public void Render_documents_stress_options()
    {
        var banner = OpusAlphaCliHelp.Render(reason: string.Empty);

        banner.Should().Contain("--iterations");
        banner.Should().Contain("--stress-dir");
        banner.Should().Contain("--known-issues");
    }

    [Fact]
    public void Render_documents_stress_fault_injection_options()
    {
        var banner = OpusAlphaCliHelp.Render(reason: string.Empty);

        banner.Should().Contain("--inject-loss");
        banner.Should().Contain("--inject-latency-ms");
        banner.Should().Contain("--inject-seed");
        banner.Should().Contain("--inject-peers");
        banner.Should().Contain("--inject-packets");
        banner.Should().Contain("--inject-payload");
        banner.Should().Contain("--inject-drop-tolerance");
    }

    [Fact]
    public void Render_documents_inbound_fault_injection_options()
    {
        var banner = OpusAlphaCliHelp.Render(reason: string.Empty);

        banner.Should().Contain("--inject-inbound-loss");
        banner.Should().Contain("--inject-inbound-latency-ms");
        banner.Should().Contain("--inject-inbound-seed");
        banner.Should().Contain("--inject-inbound-drop-tolerance");
    }

    [Fact]
    public void Render_documents_known_issues_subcommands()
    {
        var banner = OpusAlphaCliHelp.Render(reason: string.Empty);

        banner.Should().Contain("opus-alpha-host known-issues-merge");
        banner.Should().Contain("opus-alpha-host known-issues-diff");
        banner.Should().Contain("--base");
        banner.Should().Contain("--overlay");
        banner.Should().Contain("--left");
        banner.Should().Contain("--right");
        banner.Should().Contain("--format");
        banner.Should().Contain("--output");
    }

    [Fact]
    public void Render_with_reason_prefixes_error_line()
    {
        var banner = OpusAlphaCliHelp.Render("--scene expects 'small' or 'large'");

        banner.Should().StartWith("error: --scene expects 'small' or 'large'");
    }

    [Fact]
    public void Render_includes_option_documentation_for_known_options()
    {
        var banner = OpusAlphaCliHelp.Render(reason: string.Empty);

        banner.Should().Contain("--scene");
        banner.Should().Contain("--frames");
        banner.Should().Contain("--screenshot-frame");
        banner.Should().Contain("--package");
        banner.Should().Contain("--reference");
        banner.Should().Contain("--peers");
        banner.Should().Contain("--packets");
        banner.Should().Contain("--payload");
    }

    [Fact]
    public void Write_streams_banner_into_text_writer()
    {
        using var writer = new StringWriter();

        OpusAlphaCliHelp.Write(writer, "reason");

        writer.ToString().Should().StartWith("error: reason");
    }
}
