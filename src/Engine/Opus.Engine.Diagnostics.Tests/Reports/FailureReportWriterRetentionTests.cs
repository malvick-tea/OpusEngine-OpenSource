using System;
using System.Globalization;
using System.IO;
using FluentAssertions;
using Opus.Engine.Diagnostics.Reports;
using Opus.Foundation;
using Xunit;

namespace Opus.Engine.Diagnostics.Tests.Reports;

public sealed class FailureReportWriterRetentionTests
{
    private static readonly DateTimeOffset NowUtc = new(2026, 5, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Writer_sweeps_old_pairs_before_writing_a_new_pair()
    {
        using var temp = TempDirectory.Create();
        SeedStalePair(temp.Path, NowUtc.AddDays(-90).UtcDateTime);
        var options = new FailureReportWriterOptions(
            temp.Path,
            new DiagnosticsArtifactRetentionPolicy(MaxPairCount: 0, MaxAge: TimeSpan.FromDays(30)));
        var writer = new FailureReportWriter(options, new FrozenClock(NowUtc));

        var result = writer.Write(NewReport());

        result.Succeeded.Should().BeTrue();
        var jsonFiles = Directory.GetFiles(temp.Path, "*.json");
        jsonFiles.Should().ContainSingle(
            "the stale failure-report pair must be swept before the new pair lands.");
    }

    [Fact]
    public void Writer_keeps_existing_pairs_when_retention_is_disabled()
    {
        using var temp = TempDirectory.Create();
        SeedStalePair(temp.Path, NowUtc.AddDays(-90).UtcDateTime);
        var writer = new FailureReportWriter(
            new FailureReportWriterOptions(temp.Path),
            new FrozenClock(NowUtc));

        var result = writer.Write(NewReport());

        result.Succeeded.Should().BeTrue();
        Directory.GetFiles(temp.Path, "*.json").Length.Should().Be(
            2,
            "without an active retention policy the writer must not delete prior evidence.");
    }

    private static FailureReport NewReport() => FailureReport.Capture(
        FailureReportKind.StartupFailure,
        NowUtc,
        BuildInfo.Current,
        FailureReportAdapterSnapshot.Unavailable,
        new[] { "last line" },
        screenshotPath: null,
        new InvalidOperationException("boom"));

    private static void SeedStalePair(string directory, DateTime mtimeUtc)
    {
        var stem = string.Create(
            CultureInfo.InvariantCulture,
            $"opus-startupfailure-20260101-000000000-{Guid.NewGuid():N}");
        var jsonPath = Path.Combine(directory, stem + ".json");
        var textPath = Path.Combine(directory, stem + ".txt");
        File.WriteAllText(jsonPath, "{}");
        File.WriteAllText(textPath, "Opus failure report");
        File.SetLastWriteTimeUtc(jsonPath, mtimeUtc);
        File.SetLastWriteTimeUtc(textPath, mtimeUtc);
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

    private sealed class TempDirectory : IDisposable
    {
        private TempDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TempDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "opus-failure-retention-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TempDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
