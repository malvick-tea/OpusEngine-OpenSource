using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Opus.Engine.AlphaHarness.Smoke;
using Opus.Engine.AlphaStress.FramePacing;
using Opus.Engine.AlphaStress.Memory;
using Opus.Engine.AlphaStress.Network;
using Opus.Engine.AlphaStress.Stress;
using Xunit;

namespace Opus.Engine.AlphaStress.Tests.Stress;

public sealed class AlphaStressReportWriterTests : IDisposable
{
    private readonly string _root;

    public AlphaStressReportWriterTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "opus-alpha-stress-rw-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Write_persists_paired_json_and_text_artifacts()
    {
        var writer = new AlphaStressReportWriter(new AlphaStressReportWriterOptions(_root));
        var outcome = BuildOutcome();

        var result = writer.Write(outcome);

        result.IsSuccess.Should().BeTrue();
        File.Exists(result.JsonPath!).Should().BeTrue();
        File.Exists(result.TextPath!).Should().BeTrue();
        Path.GetExtension(result.JsonPath!).Should().Be(".json");
        Path.GetExtension(result.TextPath!).Should().Be(".txt");
    }

    [Fact]
    public void Write_text_payload_contains_summary_rows()
    {
        var writer = new AlphaStressReportWriter(new AlphaStressReportWriterOptions(_root));
        var outcome = BuildOutcome();

        var result = writer.Write(outcome);
        var text = File.ReadAllText(result.TextPath!);

        text.Should().Contain("stressName:");
        text.Should().Contain("framePacing:");
        text.Should().Contain("memory:");
        text.Should().Contain("network:");
        text.Should().Contain("issues:");
    }

    [Fact]
    public void Write_creates_directory_when_missing()
    {
        var nested = Path.Combine(_root, "nested-stress");
        var writer = new AlphaStressReportWriter(new AlphaStressReportWriterOptions(nested));

        var result = writer.Write(BuildOutcome());

        result.IsSuccess.Should().BeTrue();
        Directory.Exists(nested).Should().BeTrue();
    }

    [Fact]
    public void Write_leaves_no_temp_files_on_success()
    {
        var writer = new AlphaStressReportWriter(new AlphaStressReportWriterOptions(_root));

        writer.Write(BuildOutcome());

        Directory.EnumerateFiles(_root, "*.tmp").Should().BeEmpty();
    }

    [Fact]
    public void Write_null_outcome_throws()
    {
        var writer = new AlphaStressReportWriter(new AlphaStressReportWriterOptions(_root));

        var act = () => writer.Write(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_null_options_throws()
    {
        var act = () => new AlphaStressReportWriter(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Write_invalid_options_returns_structured_issue()
    {
        var writer = new AlphaStressReportWriter(new AlphaStressReportWriterOptions(string.Empty));

        var result = writer.Write(BuildOutcome());

        result.IsSuccess.Should().BeFalse();
        result.Issue.Should().NotBeNull();
        result.Issue!.Code.Should().Be(AlphaStressDiagnosticCodes.StressReportWriteFailed);
    }

    [Fact]
    public void Write_text_includes_issues_when_present()
    {
        var writer = new AlphaStressReportWriter(new AlphaStressReportWriterOptions(_root));
        var profile = AlphaStressProfile.Default;
        var issues = new[]
        {
            AlphaStressIssue.Global(AlphaStressIssueCode.BudgetExceeded, "budget exceeded message", DateTimeOffset.UtcNow),
        };
        var outcome = AlphaStressOutcome.Create(
            profile,
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(1),
            Array.Empty<AlphaStressIterationOutcome>(),
            FramePacingSummary.Empty(profile.FramePacing.HitchThreshold),
            MemoryProbeSummary.Empty,
            AlphaStressNetworkSummary.Empty,
            issues);

        var result = writer.Write(outcome);
        var text = File.ReadAllText(result.TextPath!);

        text.Should().Contain(AlphaStressDiagnosticCodes.StressBudgetExceeded);
        text.Should().Contain("budget exceeded message");
    }

    [Fact]
    public void Multiple_writes_produce_unique_files()
    {
        var writer = new AlphaStressReportWriter(new AlphaStressReportWriterOptions(_root));

        var first = writer.Write(BuildOutcome());
        var second = writer.Write(BuildOutcome());

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        first.JsonPath.Should().NotBe(second.JsonPath);
        Directory.EnumerateFiles(_root, "*.json").Should().HaveCount(2);
    }

    private static AlphaStressOutcome BuildOutcome()
    {
        var profile = AlphaStressProfile.Default;
        var pacing = new FramePacingAggregator(profile.FramePacing.HitchThreshold);
        var baseTime = DateTimeOffset.UtcNow;
        pacing.Record(new FramePacingObservation(1, baseTime, TimeSpan.FromMilliseconds(10)));
        pacing.Record(new FramePacingObservation(2, baseTime, TimeSpan.FromMilliseconds(20)));
        var memory = new MemoryProbeAggregator();
        memory.Record(new MemoryProbeSample(baseTime, 1024, 4096, 0, 0, 0));
        memory.Record(new MemoryProbeSample(baseTime.AddSeconds(1), 2048, 8192, 0, 0, 0));
        var iteration = new AlphaStressIterationOutcome(
            IterationIndex: 0,
            StartedAtUtc: baseTime,
            ElapsedWallClock: TimeSpan.FromMilliseconds(20),
            SmokeOutcome: AlphaSmokeOutcome.Create(
                profile.IterationProfile,
                baseTime,
                TimeSpan.FromMilliseconds(20),
                framesStepped: profile.IterationProfile.FrameTarget,
                meanCpuFrameTime: TimeSpan.FromMilliseconds(10),
                maxCpuFrameTime: TimeSpan.FromMilliseconds(15),
                p95CpuFrameTime: TimeSpan.FromMilliseconds(12),
                screenshotPath: null,
                issues: Array.Empty<AlphaSmokeIssue>()),
            FramePacing: pacing.BuildSummary(),
            UnhandledExceptionMessage: null);
        return AlphaStressOutcome.Create(
            profile,
            baseTime,
            TimeSpan.FromMilliseconds(50),
            new[] { iteration },
            pacing.BuildSummary(),
            memory.BuildSummary(),
            AlphaStressNetworkSummary.Empty,
            Array.Empty<AlphaStressIssue>());
    }
}
