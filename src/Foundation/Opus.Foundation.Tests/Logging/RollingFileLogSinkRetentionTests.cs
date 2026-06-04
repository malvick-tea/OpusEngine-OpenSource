using System;
using System.Globalization;
using System.IO;
using FluentAssertions;
using Opus.Foundation;
using Xunit;

namespace Opus.Foundation.Tests.Logging;

public sealed class RollingFileLogSinkRetentionTests
{
    private const string Prefix = "test-opus";

    [Fact]
    public void Session_open_deletes_files_past_max_count()
    {
        using var temp = TempDirectory.Create();
        SeedPriorSessionFiles(temp.Path, count: 6);

        using var sink = OpenSinkWithRetention(temp.Path, maxFileCount: 3, maxAge: TimeSpan.Zero);

        var remaining = Directory.GetFiles(temp.Path, Prefix + "-*.log");
        remaining.Length.Should().Be(
            3,
            "the sweep should keep MaxFileCount-1 prior files plus the new session log.");
    }

    [Fact]
    public void Session_open_deletes_files_past_max_age()
    {
        using var temp = TempDirectory.Create();
        SeedPriorSessionFiles(
            temp.Path,
            count: 4,
            mtimeUtc: new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc));

        using var sink = OpenSinkWithRetention(
            temp.Path,
            maxFileCount: 0,
            maxAge: TimeSpan.FromDays(7));

        var remaining = Directory.GetFiles(temp.Path, Prefix + "-*.log");
        remaining.Should().ContainSingle(
            "every prior file was older than the seven-day age limit; only the new session log survives.");
    }

    [Fact]
    public void Disabled_policy_keeps_every_prior_file()
    {
        using var temp = TempDirectory.Create();
        SeedPriorSessionFiles(temp.Path, count: 5);

        using var sink = new RollingFileLogSink(NewOptions(temp.Path, retention: null), FrozenClock.Instance);

        Directory.GetFiles(temp.Path, Prefix + "-*.log").Length.Should().Be(
            6,
            "with retention disabled, only the new session log is added.");
    }

    [Fact]
    public void Sweep_ignores_files_owned_by_other_prefixes()
    {
        using var temp = TempDirectory.Create();
        SeedPriorSessionFiles(temp.Path, count: 4);
        var foreignPath = Path.Combine(temp.Path, "foreign-log.log");
        File.WriteAllText(foreignPath, "foreign");

        using var sink = OpenSinkWithRetention(temp.Path, maxFileCount: 2, maxAge: TimeSpan.Zero);

        File.Exists(foreignPath).Should().BeTrue(
            "the sweeper must only touch files matching the configured prefix.");
    }

    private static RollingFileLogSink OpenSinkWithRetention(string directory, int maxFileCount, TimeSpan maxAge)
    {
        var retention = new RollingLogRetentionPolicy(maxFileCount, maxAge);
        return new RollingFileLogSink(NewOptions(directory, retention), FrozenClock.Instance);
    }

    private static RollingLogSinkOptions NewOptions(string directory, RollingLogRetentionPolicy? retention) =>
        RollingLogSinkOptions.ForDirectory(directory) with
        {
            FileNamePrefix = Prefix,
            MaxFileBytes = 4_096,
            MaxTailEntries = 4,
            Retention = retention,
        };

    private static void SeedPriorSessionFiles(string directory, int count, DateTime? mtimeUtc = null)
    {
        for (var i = 0; i < count; i++)
        {
            var path = Path.Combine(
                directory,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{Prefix}-20260101-0000{i:D2}.log"));
            File.WriteAllText(path, "stale-session");
            if (mtimeUtc is { } mtime)
            {
                File.SetLastWriteTimeUtc(path, mtime);
            }
            else
            {
                File.SetLastWriteTimeUtc(path, new DateTime(2026, 5, 25, 12, 0, 0, DateTimeKind.Utc));
            }
        }
    }

    private sealed class FrozenClock : TimeProvider
    {
        public static FrozenClock Instance { get; } = new();

        public override DateTimeOffset GetUtcNow() =>
            new(2026, 5, 26, 12, 0, 0, TimeSpan.Zero);

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
                "opus-rolling-log-retention-tests-" + Guid.NewGuid().ToString("N"));
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
