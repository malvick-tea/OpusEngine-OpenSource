using System;
using System.Globalization;
using System.IO;
using FluentAssertions;
using Opus.Engine.AlphaHarness.Smoke;
using Opus.Engine.Diagnostics.Reports;
using Xunit;

namespace Opus.Engine.AlphaHarness.Tests.Smoke;

public sealed class AlphaSmokeReportWriterRetentionTests : IDisposable
{
    private static readonly DateTimeOffset NowUtc = new(2026, 5, 26, 12, 0, 0, TimeSpan.Zero);
    private readonly string _root;

    public AlphaSmokeReportWriterRetentionTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "opus-alpha-smoke-retention-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public void Active_retention_sweeps_stale_smoke_pairs_before_writing()
    {
        SeedStalePair(_root, NowUtc.AddDays(-90).UtcDateTime);
        var options = new AlphaSmokeReportWriterOptions(
            _root,
            new DiagnosticsArtifactRetentionPolicy(MaxPairCount: 0, MaxAge: TimeSpan.FromDays(30)));
        var writer = new AlphaSmokeReportWriter(options, new FrozenClock(NowUtc));

        var result = writer.Write(BuildCleanOutcome());

        result.Succeeded.Should().BeTrue();
        Directory.GetFiles(_root, "*.json").Length.Should().Be(
            1,
            "the stale smoke pair must be swept before the new pair lands.");
    }

    [Fact]
    public void Disabled_retention_keeps_stale_smoke_pairs()
    {
        SeedStalePair(_root, NowUtc.AddDays(-90).UtcDateTime);
        var writer = new AlphaSmokeReportWriter(
            new AlphaSmokeReportWriterOptions(_root),
            new FrozenClock(NowUtc));

        writer.Write(BuildCleanOutcome());

        Directory.GetFiles(_root, "*.json").Length.Should().Be(
            2,
            "the default writer keeps prior evidence intact until an operator opts in.");
    }

    private static void SeedStalePair(string directory, DateTime mtimeUtc)
    {
        var stem = string.Create(
            CultureInfo.InvariantCulture,
            $"{AlphaSmokeReportWriterOptions.ArtifactStemPrefix}20260101-000000000-{Guid.NewGuid():N}");
        var jsonPath = Path.Combine(directory, stem + ".json");
        var textPath = Path.Combine(directory, stem + ".txt");
        File.WriteAllText(jsonPath, "{}");
        File.WriteAllText(textPath, "Opus alpha smoke report");
        File.SetLastWriteTimeUtc(jsonPath, mtimeUtc);
        File.SetLastWriteTimeUtc(textPath, mtimeUtc);
    }

    private static AlphaSmokeOutcome BuildCleanOutcome() => AlphaSmokeOutcome.Create(
        profile: AlphaSmokeProfile.Default,
        startedAtUtc: NowUtc,
        elapsedWallClock: TimeSpan.FromMilliseconds(120),
        framesStepped: AlphaSmokeProfile.DefaultFrameTarget,
        meanCpuFrameTime: TimeSpan.FromMilliseconds(8),
        maxCpuFrameTime: TimeSpan.FromMilliseconds(16),
        p95CpuFrameTime: TimeSpan.FromMilliseconds(12),
        screenshotPath: null,
        issues: Array.Empty<AlphaSmokeIssue>());

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
