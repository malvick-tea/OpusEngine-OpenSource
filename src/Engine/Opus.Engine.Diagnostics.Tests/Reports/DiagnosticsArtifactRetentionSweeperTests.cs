using System;
using System.Globalization;
using System.IO;
using FluentAssertions;
using Opus.Engine.Diagnostics.Reports;
using Xunit;

namespace Opus.Engine.Diagnostics.Tests.Reports;

public sealed class DiagnosticsArtifactRetentionSweeperTests
{
    private const string Prefix = "opus-test-";
    private static readonly DateTimeOffset NowUtc = new(2026, 5, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Disabled_policy_is_a_no_op()
    {
        using var temp = TempDirectory.Create();
        SeedPair(temp.Path, "001", NowUtc.AddDays(-100).UtcDateTime);

        DiagnosticsArtifactRetentionSweeper.Sweep(
            temp.Path,
            Prefix,
            DiagnosticsArtifactRetentionPolicy.Disabled,
            NowUtc);

        Directory.GetFiles(temp.Path).Should().HaveCount(2);
    }

    [Fact]
    public void Count_rule_keeps_newest_pairs_and_deletes_the_rest()
    {
        using var temp = TempDirectory.Create();
        SeedPair(temp.Path, "001", NowUtc.AddHours(-5).UtcDateTime);
        SeedPair(temp.Path, "002", NowUtc.AddHours(-4).UtcDateTime);
        SeedPair(temp.Path, "003", NowUtc.AddHours(-3).UtcDateTime);
        SeedPair(temp.Path, "004", NowUtc.AddHours(-2).UtcDateTime);

        DiagnosticsArtifactRetentionSweeper.Sweep(
            temp.Path,
            Prefix,
            new DiagnosticsArtifactRetentionPolicy(MaxPairCount: 3, MaxAge: TimeSpan.Zero),
            NowUtc);

        var remaining = Directory.GetFiles(temp.Path);
        remaining.Should().HaveCount(
            4,
            "MaxPairCount=3 keeps two prior pairs to make room for a third pair the writer is about to add.");
        File.Exists(StemPath(temp.Path, "001", ".json")).Should().BeFalse();
        File.Exists(StemPath(temp.Path, "001", ".txt")).Should().BeFalse();
        File.Exists(StemPath(temp.Path, "004", ".json")).Should().BeTrue();
        File.Exists(StemPath(temp.Path, "004", ".txt")).Should().BeTrue();
    }

    [Fact]
    public void Age_rule_deletes_pairs_older_than_threshold()
    {
        using var temp = TempDirectory.Create();
        SeedPair(temp.Path, "old", NowUtc.AddDays(-40).UtcDateTime);
        SeedPair(temp.Path, "fresh", NowUtc.AddDays(-1).UtcDateTime);

        DiagnosticsArtifactRetentionSweeper.Sweep(
            temp.Path,
            Prefix,
            new DiagnosticsArtifactRetentionPolicy(MaxPairCount: 0, MaxAge: TimeSpan.FromDays(30)),
            NowUtc);

        File.Exists(StemPath(temp.Path, "old", ".json")).Should().BeFalse();
        File.Exists(StemPath(temp.Path, "old", ".txt")).Should().BeFalse();
        File.Exists(StemPath(temp.Path, "fresh", ".json")).Should().BeTrue();
        File.Exists(StemPath(temp.Path, "fresh", ".txt")).Should().BeTrue();
    }

    [Fact]
    public void Pair_age_uses_newest_member_to_resist_clock_skew()
    {
        using var temp = TempDirectory.Create();
        var jsonPath = StemPath(temp.Path, "skew", ".json");
        var textPath = StemPath(temp.Path, "skew", ".txt");
        File.WriteAllText(jsonPath, "json");
        File.WriteAllText(textPath, "text");
        File.SetLastWriteTimeUtc(jsonPath, NowUtc.AddDays(-90).UtcDateTime);
        File.SetLastWriteTimeUtc(textPath, NowUtc.AddDays(-1).UtcDateTime);

        DiagnosticsArtifactRetentionSweeper.Sweep(
            temp.Path,
            Prefix,
            new DiagnosticsArtifactRetentionPolicy(MaxPairCount: 0, MaxAge: TimeSpan.FromDays(30)),
            NowUtc);

        File.Exists(jsonPath).Should().BeTrue(
            "the text half is fresh so the pair survives even though the json half mtime is ancient.");
        File.Exists(textPath).Should().BeTrue();
    }

    [Fact]
    public void Sweep_does_not_touch_files_outside_the_stem_prefix()
    {
        using var temp = TempDirectory.Create();
        SeedPair(temp.Path, "001", NowUtc.AddDays(-100).UtcDateTime);
        var foreign = Path.Combine(temp.Path, "foreign-report.json");
        File.WriteAllText(foreign, "foreign");

        DiagnosticsArtifactRetentionSweeper.Sweep(
            temp.Path,
            Prefix,
            new DiagnosticsArtifactRetentionPolicy(MaxPairCount: 1, MaxAge: TimeSpan.Zero),
            NowUtc);

        File.Exists(foreign).Should().BeTrue(
            "the sweeper must only delete files matching the configured stem prefix.");
    }

    [Fact]
    public void Sweep_is_safe_on_missing_directory()
    {
        var missing = Path.Combine(Path.GetTempPath(), "opus-no-such-dir-" + Guid.NewGuid().ToString("N"));

        var act = () => DiagnosticsArtifactRetentionSweeper.Sweep(
            missing,
            Prefix,
            DiagnosticsArtifactRetentionPolicy.Default,
            NowUtc);

        act.Should().NotThrow("a missing directory is a normal first-run state.");
    }

    [Fact]
    public void Sweep_collapses_orphaned_halves_into_single_pair_entry()
    {
        using var temp = TempDirectory.Create();
        var orphanJson = StemPath(temp.Path, "orphan", ".json");
        File.WriteAllText(orphanJson, "json");
        File.SetLastWriteTimeUtc(orphanJson, NowUtc.AddDays(-2).UtcDateTime);
        SeedPair(temp.Path, "complete", NowUtc.AddHours(-1).UtcDateTime);

        DiagnosticsArtifactRetentionSweeper.Sweep(
            temp.Path,
            Prefix,
            new DiagnosticsArtifactRetentionPolicy(MaxPairCount: 2, MaxAge: TimeSpan.Zero),
            NowUtc);

        File.Exists(orphanJson).Should().BeFalse(
            "an orphaned half participates in the count rule and gets swept when it is older.");
        File.Exists(StemPath(temp.Path, "complete", ".json")).Should().BeTrue();
        File.Exists(StemPath(temp.Path, "complete", ".txt")).Should().BeTrue();
    }

    [Fact]
    public void Count_rule_retires_attached_screenshot_with_its_unit()
    {
        using var temp = TempDirectory.Create();
        SeedTriple(temp.Path, "001", NowUtc.AddHours(-5).UtcDateTime);
        SeedTriple(temp.Path, "002", NowUtc.AddHours(-4).UtcDateTime);
        SeedTriple(temp.Path, "003", NowUtc.AddHours(-3).UtcDateTime);

        DiagnosticsArtifactRetentionSweeper.Sweep(
            temp.Path,
            Prefix,
            new DiagnosticsArtifactRetentionPolicy(MaxPairCount: 2, MaxAge: TimeSpan.Zero),
            NowUtc);

        File.Exists(StemPath(temp.Path, "001", ".json")).Should().BeFalse();
        File.Exists(StemPath(temp.Path, "001", ".txt")).Should().BeFalse();
        File.Exists(StemPath(temp.Path, "001", ".png")).Should().BeFalse(
            "the oldest unit's screenshot is retired together with its report halves.");
        File.Exists(StemPath(temp.Path, "003", ".json")).Should().BeTrue();
        File.Exists(StemPath(temp.Path, "003", ".png")).Should().BeTrue();
    }

    [Fact]
    public void Age_rule_retires_attached_screenshot_with_its_unit()
    {
        using var temp = TempDirectory.Create();
        SeedTriple(temp.Path, "old", NowUtc.AddDays(-40).UtcDateTime);
        SeedTriple(temp.Path, "fresh", NowUtc.AddDays(-1).UtcDateTime);

        DiagnosticsArtifactRetentionSweeper.Sweep(
            temp.Path,
            Prefix,
            new DiagnosticsArtifactRetentionPolicy(MaxPairCount: 0, MaxAge: TimeSpan.FromDays(30)),
            NowUtc);

        File.Exists(StemPath(temp.Path, "old", ".png")).Should().BeFalse();
        File.Exists(StemPath(temp.Path, "fresh", ".json")).Should().BeTrue();
        File.Exists(StemPath(temp.Path, "fresh", ".png")).Should().BeTrue();
    }

    [Fact]
    public void Sweep_leaves_screenshot_without_a_report_sibling_untouched()
    {
        using var temp = TempDirectory.Create();
        var lonePng = StemPath(temp.Path, "lone", ".png");
        File.WriteAllBytes(lonePng, new byte[] { 1, 2, 3 });
        File.SetLastWriteTimeUtc(lonePng, NowUtc.AddDays(-100).UtcDateTime);
        SeedTriple(temp.Path, "real", NowUtc.AddHours(-1).UtcDateTime);

        DiagnosticsArtifactRetentionSweeper.Sweep(
            temp.Path,
            Prefix,
            new DiagnosticsArtifactRetentionPolicy(MaxPairCount: 1, MaxAge: TimeSpan.Zero),
            NowUtc);

        File.Exists(lonePng).Should().BeTrue(
            "a screenshot with no report half is not a retention unit the sweeper owns.");
        File.Exists(StemPath(temp.Path, "real", ".json")).Should().BeFalse();
    }

    private static void SeedPair(string directory, string suffix, DateTime mtimeUtc)
    {
        var jsonPath = StemPath(directory, suffix, ".json");
        var textPath = StemPath(directory, suffix, ".txt");
        File.WriteAllText(jsonPath, "json");
        File.WriteAllText(textPath, "text");
        File.SetLastWriteTimeUtc(jsonPath, mtimeUtc);
        File.SetLastWriteTimeUtc(textPath, mtimeUtc);
    }

    private static void SeedTriple(string directory, string suffix, DateTime mtimeUtc)
    {
        SeedPair(directory, suffix, mtimeUtc);
        var screenshotPath = StemPath(directory, suffix, ".png");
        File.WriteAllBytes(screenshotPath, new byte[] { 1, 2, 3, 4 });
        File.SetLastWriteTimeUtc(screenshotPath, mtimeUtc);
    }

    private static string StemPath(string directory, string suffix, string extension) =>
        Path.Combine(directory, string.Create(CultureInfo.InvariantCulture, $"{Prefix}{suffix}{extension}"));

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
                "opus-artifact-retention-tests-" + Guid.NewGuid().ToString("N"));
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
