using FluentAssertions;
using Opus.App.OpusAlpha.Cli;
using Xunit;

namespace Opus.App.OpusAlpha.Tests.Cli;

public sealed class OpusAlphaCliParserKnownIssuesTests
{
    [Fact]
    public void Known_issues_merge_subcommand_routes_to_merge_mode()
    {
        var args = OpusAlphaCliParser.Parse(new[]
        {
            "known-issues-merge",
            "--base", "base.json",
            "--overlay", "overlay.json",
            "--output", "merged.json",
        });

        args.Mode.Should().Be(OpusAlphaMode.KnownIssuesMerge);
        args.KnownIssuesBasePath.Should().Be("base.json");
        args.KnownIssuesOverlayPath.Should().Be("overlay.json");
        args.KnownIssuesOutputPath.Should().Be("merged.json");
    }

    [Fact]
    public void Known_issues_diff_subcommand_routes_to_diff_mode()
    {
        var args = OpusAlphaCliParser.Parse(new[]
        {
            "known-issues-diff",
            "--left", "left.json",
            "--right", "right.json",
        });

        args.Mode.Should().Be(OpusAlphaMode.KnownIssuesDiff);
        args.KnownIssuesLeftPath.Should().Be("left.json");
        args.KnownIssuesRightPath.Should().Be("right.json");
        args.KnownIssuesDiffFormat.Should().Be(KnownIssuesDiffFormat.Text);
    }

    [Fact]
    public void Diff_format_json_overrides_default_text()
    {
        var args = OpusAlphaCliParser.Parse(new[]
        {
            "known-issues-diff",
            "--left", "a.json",
            "--right", "b.json",
            "--format", "json",
        });

        args.KnownIssuesDiffFormat.Should().Be(KnownIssuesDiffFormat.Json);
    }

    [Fact]
    public void Diff_format_rejects_unknown_value()
    {
        var args = OpusAlphaCliParser.Parse(new[]
        {
            "known-issues-diff",
            "--left", "a.json",
            "--right", "b.json",
            "--format", "yaml",
        });

        args.Mode.Should().Be(OpusAlphaMode.Help);
        args.HelpReason.Should().Contain("--format");
    }

    [Fact]
    public void Known_issues_merge_output_routes_via_shared_option()
    {
        var args = OpusAlphaCliParser.Parse(new[]
        {
            "known-issues-merge",
            "--base", "base.json",
            "--overlay", "overlay.json",
            "--output", "C:/out/merged.json",
        });

        args.KnownIssuesOutputPath.Should().Be("C:/out/merged.json");
    }

    [Fact]
    public void Diff_output_is_optional_when_unspecified_diff_prints_to_stdout()
    {
        var args = OpusAlphaCliParser.Parse(new[]
        {
            "known-issues-diff",
            "--left", "a.json",
            "--right", "b.json",
        });

        args.KnownIssuesOutputPath.Should().BeNull();
    }

    [Fact]
    public void Missing_option_value_routes_to_help()
    {
        var args = OpusAlphaCliParser.Parse(new[]
        {
            "known-issues-merge",
            "--base",
        });

        args.Mode.Should().Be(OpusAlphaMode.Help);
        args.HelpReason.Should().Contain("--base");
    }
}
