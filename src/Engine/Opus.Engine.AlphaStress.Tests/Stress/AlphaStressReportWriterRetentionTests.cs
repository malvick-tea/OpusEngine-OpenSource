using System;
using System.Globalization;
using System.IO;
using FluentAssertions;
using Opus.Engine.AlphaHarness.Smoke;
using Opus.Engine.AlphaStress.FramePacing;
using Opus.Engine.AlphaStress.Memory;
using Opus.Engine.AlphaStress.Network;
using Opus.Engine.AlphaStress.Stress;
using Opus.Engine.Diagnostics.Reports;
using Xunit;

namespace Opus.Engine.AlphaStress.Tests.Stress;

public sealed class AlphaStressReportWriterRetentionTests : IDisposable
{
    private static readonly DateTimeOffset NowUtc = new(2026, 5, 26, 12, 0, 0, TimeSpan.Zero);
    private readonly string _root;

    public AlphaStressReportWriterRetentionTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "opus-alpha-stress-rw-retention-" + Guid.NewGuid().ToString("N"));
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
    public void Active_retention_sweeps_stale_stress_pairs_before_writing()
    {
        SeedStalePair(_root, NowUtc.AddDays(-90).UtcDateTime);
        var options = new AlphaStressReportWriterOptions(
            _root,
            new DiagnosticsArtifactRetentionPolicy(MaxPairCount: 0, MaxAge: TimeSpan.FromDays(30)));
        var writer = new AlphaStressReportWriter(options, new FrozenClock(NowUtc));

        var result = writer.Write(BuildOutcome());

        result.IsSuccess.Should().BeTrue();
        Directory.GetFiles(_root, "*.json").Length.Should().Be(
            1,
            "the stale stress pair must be swept before the new one lands.");
    }

    [Fact]
    public void Disabled_retention_keeps_stale_stress_pairs()
    {
        SeedStalePair(_root, NowUtc.AddDays(-90).UtcDateTime);
        var writer = new AlphaStressReportWriter(
            new AlphaStressReportWriterOptions(_root),
            new FrozenClock(NowUtc));

        writer.Write(BuildOutcome());

        Directory.GetFiles(_root, "*.json").Length.Should().Be(
            2,
            "the default writer keeps prior evidence intact until an operator opts in.");
    }

    [Fact]
    public void Count_rule_keeps_max_pair_count_minus_one_existing_pairs()
    {
        SeedStalePair(_root, NowUtc.AddHours(-3).UtcDateTime, "001");
        SeedStalePair(_root, NowUtc.AddHours(-2).UtcDateTime, "002");
        SeedStalePair(_root, NowUtc.AddHours(-1).UtcDateTime, "003");
        var options = new AlphaStressReportWriterOptions(
            _root,
            new DiagnosticsArtifactRetentionPolicy(MaxPairCount: 2, MaxAge: TimeSpan.Zero));
        var writer = new AlphaStressReportWriter(options, new FrozenClock(NowUtc));

        var result = writer.Write(BuildOutcome());

        result.IsSuccess.Should().BeTrue();
        Directory.GetFiles(_root, "*.json").Length.Should().Be(
            2,
            "MaxPairCount=2 keeps one prior pair and the writer adds the new pair, total two.");
    }

    private static void SeedStalePair(string directory, DateTime mtimeUtc, string discriminator = "stale")
    {
        var stem = string.Create(
            CultureInfo.InvariantCulture,
            $"{AlphaStressReportWriterOptions.ArtifactStemPrefix}{discriminator}-{Guid.NewGuid():N}");
        var jsonPath = Path.Combine(directory, stem + ".json");
        var textPath = Path.Combine(directory, stem + ".txt");
        File.WriteAllText(jsonPath, "{}");
        File.WriteAllText(textPath, "Opus alpha stress report");
        File.SetLastWriteTimeUtc(jsonPath, mtimeUtc);
        File.SetLastWriteTimeUtc(textPath, mtimeUtc);
    }

    private static AlphaStressOutcome BuildOutcome()
    {
        var profile = AlphaStressProfile.Default;
        var pacing = new FramePacingAggregator(profile.FramePacing.HitchThreshold);
        var baseTime = NowUtc;
        pacing.Record(new FramePacingObservation(1, baseTime, TimeSpan.FromMilliseconds(10)));
        var memory = new MemoryProbeAggregator();
        memory.Record(new MemoryProbeSample(baseTime, 1024, 4096, 0, 0, 0));
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

    private sealed class FrozenClock : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FrozenClock(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow() => _now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
