using System;
using FluentAssertions;
using Opus.Engine.AlphaHarness.Smoke;
using Xunit;

namespace Opus.Engine.AlphaHarness.Tests.Smoke;

public sealed class AlphaSmokeOutcomeTests
{
    private static readonly DateTimeOffset FixedStart = new(2026, 5, 27, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Issue_code_diagnostic_strings_map_one_to_one()
    {
        AlphaSmokeIssueCode.HostUnavailable.ToDiagnosticCode()
            .Should().Be(AlphaHarnessDiagnosticCodes.SmokeHostUnavailable);
        AlphaSmokeIssueCode.BudgetExceeded.ToDiagnosticCode()
            .Should().Be(AlphaHarnessDiagnosticCodes.SmokeBudgetExceeded);
        AlphaSmokeIssueCode.HostStoppedEarly.ToDiagnosticCode()
            .Should().Be(AlphaHarnessDiagnosticCodes.SmokeHostStoppedEarly);
        AlphaSmokeIssueCode.UnhandledException.ToDiagnosticCode()
            .Should().Be(AlphaHarnessDiagnosticCodes.SmokeUnhandledException);
        AlphaSmokeIssueCode.ScreenshotMissing.ToDiagnosticCode()
            .Should().Be(AlphaHarnessDiagnosticCodes.SmokeScreenshotMissing);
    }

    [Fact]
    public void Create_normalises_screenshot_path_and_sorts_issues()
    {
        var later = AlphaSmokeIssue.Create(AlphaSmokeIssueCode.BudgetExceeded, "later", FixedStart.AddSeconds(2));
        var earlier = AlphaSmokeIssue.Create(AlphaSmokeIssueCode.HostStoppedEarly, "earlier", FixedStart.AddSeconds(1));

        var outcome = AlphaSmokeOutcome.Create(
            profile: AlphaSmokeProfile.Default,
            startedAtUtc: FixedStart,
            elapsedWallClock: TimeSpan.FromMilliseconds(500),
            framesStepped: 60,
            meanCpuFrameTime: TimeSpan.FromMilliseconds(8),
            maxCpuFrameTime: TimeSpan.FromMilliseconds(16),
            p95CpuFrameTime: TimeSpan.FromMilliseconds(12),
            screenshotPath: "   ",
            issues: new[] { later, earlier });

        outcome.Issues.Should().HaveCount(2);
        outcome.Issues[0].Should().BeSameAs(earlier);
        outcome.Issues[1].Should().BeSameAs(later);
        outcome.ScreenshotPath.Should().BeNull();
        outcome.IsClean.Should().BeFalse();
        outcome.ReachedFrameTarget.Should().BeTrue();
    }

    [Fact]
    public void Clean_outcome_with_no_issues_reports_clean()
    {
        var outcome = AlphaSmokeOutcome.Create(
            profile: AlphaSmokeProfile.Default,
            startedAtUtc: FixedStart,
            elapsedWallClock: TimeSpan.FromMilliseconds(100),
            framesStepped: AlphaSmokeProfile.DefaultFrameTarget,
            meanCpuFrameTime: TimeSpan.FromMilliseconds(8),
            maxCpuFrameTime: TimeSpan.FromMilliseconds(16),
            p95CpuFrameTime: TimeSpan.FromMilliseconds(12),
            screenshotPath: "C:/tmp/x.png",
            issues: Array.Empty<AlphaSmokeIssue>());

        outcome.IsClean.Should().BeTrue();
        outcome.ScreenshotPath.Should().Be("C:/tmp/x.png");
        outcome.IssuesWithCode(AlphaSmokeIssueCode.BudgetExceeded).Should().BeEmpty();
    }

    [Fact]
    public void Negative_frames_stepped_throws()
    {
        Action act = () => AlphaSmokeOutcome.Create(
            AlphaSmokeProfile.Default,
            FixedStart,
            TimeSpan.FromSeconds(1),
            framesStepped: -1,
            TimeSpan.Zero,
            TimeSpan.Zero,
            TimeSpan.Zero,
            screenshotPath: null,
            Array.Empty<AlphaSmokeIssue>());

        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*framesStepped*");
    }

    [Fact]
    public void Issue_create_rejects_empty_message()
    {
        Action act = () => AlphaSmokeIssue.Create(AlphaSmokeIssueCode.BudgetExceeded, "  ", FixedStart);

        act.Should().Throw<ArgumentException>().WithMessage("*message*");
    }
}
