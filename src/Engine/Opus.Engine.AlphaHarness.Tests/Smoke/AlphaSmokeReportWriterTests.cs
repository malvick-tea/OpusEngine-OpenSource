using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Opus.Engine.AlphaHarness.Smoke;
using Xunit;

namespace Opus.Engine.AlphaHarness.Tests.Smoke;

public sealed class AlphaSmokeReportWriterTests : IDisposable
{
    private readonly string _directory;

    public AlphaSmokeReportWriterTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"opus-alpha-smoke-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public void Write_persists_paired_json_and_text_files()
    {
        var outcome = BuildCleanOutcome();
        var writer = new AlphaSmokeReportWriter(new AlphaSmokeReportWriterOptions(_directory));

        var result = writer.Write(outcome);

        result.Succeeded.Should().BeTrue();
        result.JsonPath.Should().NotBeNullOrWhiteSpace();
        result.TextPath.Should().NotBeNullOrWhiteSpace();
        File.Exists(result.JsonPath!).Should().BeTrue();
        File.Exists(result.TextPath!).Should().BeTrue();
        var json = File.ReadAllText(result.JsonPath!);
        json.Should().Contain("\"profile\"");
        json.Should().Contain(outcome.Profile.SmokeName);
        var text = File.ReadAllText(result.TextPath!);
        text.Should().StartWith("Opus alpha smoke report");
        text.Should().Contain($"smokeName: {outcome.Profile.SmokeName}");
        text.Should().Contain("issues:");
    }

    [Fact]
    public void Write_rejects_empty_directory_in_options()
    {
        var outcome = BuildCleanOutcome();
        var writer = new AlphaSmokeReportWriter(new AlphaSmokeReportWriterOptions("  "));

        var result = writer.Write(outcome);

        result.Succeeded.Should().BeFalse();
        result.Issue.Should().NotBeNull();
        result.Issue!.Code.Should().Be(AlphaHarnessDiagnosticCodes.SmokeReportWriteFailed);
    }

    [Fact]
    public void Write_leaves_no_tmp_files_behind_after_atomic_replace()
    {
        var outcome = BuildCleanOutcome();
        var writer = new AlphaSmokeReportWriter(new AlphaSmokeReportWriterOptions(_directory));

        writer.Write(outcome);
        writer.Write(outcome);

        Directory.EnumerateFiles(_directory, "*.tmp").Should().BeEmpty();
        Directory.EnumerateFiles(_directory, "*.json").Should().HaveCount(2);
        Directory.EnumerateFiles(_directory, "*.txt").Should().HaveCount(2);
    }

    [Fact]
    public void Write_renders_issue_diagnostic_code_and_message_in_text()
    {
        var captured = new DateTimeOffset(2026, 5, 27, 9, 0, 0, TimeSpan.Zero);
        var issue = AlphaSmokeIssue.Create(AlphaSmokeIssueCode.BudgetExceeded, "budget elapsed", captured);
        var outcome = AlphaSmokeOutcome.Create(
            AlphaSmokeProfile.Default,
            captured,
            TimeSpan.FromMilliseconds(123),
            framesStepped: 12,
            TimeSpan.FromMilliseconds(8),
            TimeSpan.FromMilliseconds(16),
            TimeSpan.FromMilliseconds(12),
            screenshotPath: null,
            issues: new[] { issue });
        var writer = new AlphaSmokeReportWriter(new AlphaSmokeReportWriterOptions(_directory));

        var result = writer.Write(outcome);

        result.Succeeded.Should().BeTrue();
        var text = File.ReadAllText(result.TextPath!);
        text.Should().Contain(AlphaHarnessDiagnosticCodes.SmokeBudgetExceeded);
        text.Should().Contain("budget elapsed");
        text.Should().Contain("screenshot: none");
        text.Should().Contain("isClean: false");
    }

    [Fact]
    public void Write_throws_on_null_outcome()
    {
        var writer = new AlphaSmokeReportWriter(new AlphaSmokeReportWriterOptions(_directory));

        Action act = () => writer.Write(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Write_handles_concurrent_back_to_back_calls()
    {
        var outcome = BuildCleanOutcome();
        var writer = new AlphaSmokeReportWriter(new AlphaSmokeReportWriterOptions(_directory));

        Enumerable.Range(0, 8).Select(_ => writer.Write(outcome)).ToList()
            .Should().AllSatisfy(r => r.Succeeded.Should().BeTrue());

        Directory.EnumerateFiles(_directory, "*.json").Should().HaveCount(8);
    }

    private static AlphaSmokeOutcome BuildCleanOutcome() => AlphaSmokeOutcome.Create(
        profile: AlphaSmokeProfile.Default,
        startedAtUtc: DateTimeOffset.UtcNow,
        elapsedWallClock: TimeSpan.FromMilliseconds(120),
        framesStepped: AlphaSmokeProfile.DefaultFrameTarget,
        meanCpuFrameTime: TimeSpan.FromMilliseconds(8),
        maxCpuFrameTime: TimeSpan.FromMilliseconds(16),
        p95CpuFrameTime: TimeSpan.FromMilliseconds(12),
        screenshotPath: null,
        issues: Array.Empty<AlphaSmokeIssue>());
}
