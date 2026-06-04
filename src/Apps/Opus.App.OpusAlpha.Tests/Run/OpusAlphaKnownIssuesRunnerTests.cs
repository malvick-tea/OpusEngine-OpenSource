using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using Opus.App.OpusAlpha.Cli;
using Opus.App.OpusAlpha.Run;
using Opus.Engine.AlphaStress.KnownIssues;
using Opus.Foundation;
using Xunit;

namespace Opus.App.OpusAlpha.Tests.Run;

public sealed class OpusAlphaKnownIssuesRunnerTests : IDisposable
{
    private const int ExitClean = 0;
    private const int ExitMissingInput = 1;
    private const int ExitLoadFailed = 2;
    private const int ExitDifferent = 4;

    private readonly string _root;

    public OpusAlphaKnownIssuesRunnerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "opus-alpha-ki-runner-" + Guid.NewGuid().ToString("N"));
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
    public void Merge_missing_base_path_exits_with_missing_input()
    {
        var args = OpusAlphaArgs.WindowDefaults() with { Mode = OpusAlphaMode.KnownIssuesMerge };
        var log = new CapturingLog();

        var exit = OpusAlphaKnownIssuesRunner.RunMerge(args, log);

        exit.Should().Be(ExitMissingInput);
        log.Errors.Should().ContainMatch("*--base*");
    }

    [Fact]
    public void Merge_missing_overlay_path_exits_with_missing_input()
    {
        var args = OpusAlphaArgs.WindowDefaults() with
        {
            Mode = OpusAlphaMode.KnownIssuesMerge,
            KnownIssuesBasePath = "base.json",
        };
        var log = new CapturingLog();

        var exit = OpusAlphaKnownIssuesRunner.RunMerge(args, log);

        exit.Should().Be(ExitMissingInput);
        log.Errors.Should().ContainMatch("*--overlay*");
    }

    [Fact]
    public void Merge_missing_output_path_exits_with_missing_input()
    {
        var args = OpusAlphaArgs.WindowDefaults() with
        {
            Mode = OpusAlphaMode.KnownIssuesMerge,
            KnownIssuesBasePath = "base.json",
            KnownIssuesOverlayPath = "overlay.json",
        };
        var log = new CapturingLog();

        var exit = OpusAlphaKnownIssuesRunner.RunMerge(args, log);

        exit.Should().Be(ExitMissingInput);
        log.Errors.Should().ContainMatch("*--output*");
    }

    [Fact]
    public void Merge_missing_input_file_exits_with_load_failed()
    {
        var basePath = Path.Combine(_root, "missing-base.json");
        var overlayPath = Path.Combine(_root, "overlay.json");
        WriteLedger(overlayPath, new[] { Build("a", KnownIssueSeverity.Blocker) });
        var args = OpusAlphaArgs.WindowDefaults() with
        {
            Mode = OpusAlphaMode.KnownIssuesMerge,
            KnownIssuesBasePath = basePath,
            KnownIssuesOverlayPath = overlayPath,
            KnownIssuesOutputPath = Path.Combine(_root, "out.json"),
        };
        var log = new CapturingLog();

        var exit = OpusAlphaKnownIssuesRunner.RunMerge(args, log);

        exit.Should().Be(ExitLoadFailed);
    }

    [Fact]
    public void Merge_writes_overlay_winning_union_to_output_file()
    {
        var basePath = Path.Combine(_root, "base.json");
        var overlayPath = Path.Combine(_root, "overlay.json");
        var outputPath = Path.Combine(_root, "merged.json");
        WriteLedger(basePath, new[]
        {
            Build("shared", KnownIssueSeverity.Blocker, KnownIssueStatus.Open),
            Build("base-only", KnownIssueSeverity.PostAlpha),
        });
        WriteLedger(overlayPath, new[]
        {
            Build("shared", KnownIssueSeverity.MustFix, KnownIssueStatus.Closed),
            Build("overlay-only", KnownIssueSeverity.MustFix),
        });
        var args = OpusAlphaArgs.WindowDefaults() with
        {
            Mode = OpusAlphaMode.KnownIssuesMerge,
            KnownIssuesBasePath = basePath,
            KnownIssuesOverlayPath = overlayPath,
            KnownIssuesOutputPath = outputPath,
        };
        var log = new CapturingLog();

        var exit = OpusAlphaKnownIssuesRunner.RunMerge(args, log);

        exit.Should().Be(ExitClean);
        var loaded = OpusAlphaKnownIssuesLoader.Load(outputPath, log);
        loaded.TotalCount.Should().Be(3);
        loaded.Records.Should().ContainSingle(r => r.Id == "shared" && r.Severity == KnownIssueSeverity.MustFix);
    }

    [Fact]
    public void Diff_missing_left_path_exits_with_missing_input()
    {
        var args = OpusAlphaArgs.WindowDefaults() with { Mode = OpusAlphaMode.KnownIssuesDiff };
        var log = new CapturingLog();

        var exit = OpusAlphaKnownIssuesRunner.RunDiff(args, log);

        exit.Should().Be(ExitMissingInput);
        log.Errors.Should().ContainMatch("*--left*");
    }

    [Fact]
    public void Diff_missing_right_path_exits_with_missing_input()
    {
        var args = OpusAlphaArgs.WindowDefaults() with
        {
            Mode = OpusAlphaMode.KnownIssuesDiff,
            KnownIssuesLeftPath = "left.json",
        };
        var log = new CapturingLog();

        var exit = OpusAlphaKnownIssuesRunner.RunDiff(args, log);

        exit.Should().Be(ExitMissingInput);
        log.Errors.Should().ContainMatch("*--right*");
    }

    [Fact]
    public void Diff_with_identical_ledgers_exits_clean()
    {
        var leftPath = Path.Combine(_root, "left.json");
        var rightPath = Path.Combine(_root, "right.json");
        var records = new[] { Build("a", KnownIssueSeverity.Blocker) };
        WriteLedger(leftPath, records);
        WriteLedger(rightPath, records);
        var args = OpusAlphaArgs.WindowDefaults() with
        {
            Mode = OpusAlphaMode.KnownIssuesDiff,
            KnownIssuesLeftPath = leftPath,
            KnownIssuesRightPath = rightPath,
            KnownIssuesOutputPath = Path.Combine(_root, "diff.txt"),
        };
        var log = new CapturingLog();

        var exit = OpusAlphaKnownIssuesRunner.RunDiff(args, log);

        exit.Should().Be(ExitClean);
    }

    [Fact]
    public void Diff_with_changes_exits_with_different_code()
    {
        var leftPath = Path.Combine(_root, "left.json");
        var rightPath = Path.Combine(_root, "right.json");
        WriteLedger(leftPath, new[] { Build("a", KnownIssueSeverity.Blocker) });
        WriteLedger(rightPath, new[] { Build("a", KnownIssueSeverity.MustFix) });
        var outputPath = Path.Combine(_root, "diff.txt");
        var args = OpusAlphaArgs.WindowDefaults() with
        {
            Mode = OpusAlphaMode.KnownIssuesDiff,
            KnownIssuesLeftPath = leftPath,
            KnownIssuesRightPath = rightPath,
            KnownIssuesOutputPath = outputPath,
        };
        var log = new CapturingLog();

        var exit = OpusAlphaKnownIssuesRunner.RunDiff(args, log);

        exit.Should().Be(ExitDifferent);
        File.Exists(outputPath).Should().BeTrue();
        File.ReadAllText(outputPath).Should().Contain("changed: 1");
    }

    [Fact]
    public void Diff_json_format_emits_structured_payload()
    {
        var leftPath = Path.Combine(_root, "left.json");
        var rightPath = Path.Combine(_root, "right.json");
        WriteLedger(leftPath, Array.Empty<KnownIssueRecord>());
        WriteLedger(rightPath, new[] { Build("a", KnownIssueSeverity.Blocker) });
        var outputPath = Path.Combine(_root, "diff.json");
        var args = OpusAlphaArgs.WindowDefaults() with
        {
            Mode = OpusAlphaMode.KnownIssuesDiff,
            KnownIssuesLeftPath = leftPath,
            KnownIssuesRightPath = rightPath,
            KnownIssuesOutputPath = outputPath,
            KnownIssuesDiffFormat = KnownIssuesDiffFormat.Json,
        };
        var log = new CapturingLog();

        var exit = OpusAlphaKnownIssuesRunner.RunDiff(args, log);

        exit.Should().Be(ExitDifferent);
        var document = JsonDocument.Parse(File.ReadAllText(outputPath));
        document.RootElement.GetProperty("added").GetInt32().Should().Be(1);
        document.RootElement.GetProperty("changes").GetArrayLength().Should().Be(1);
    }

    private static void WriteLedger(string path, IEnumerable<KnownIssueRecord> records)
    {
        var writer = new KnownIssueLedgerWriter(new KnownIssueLedgerWriterOptions(path));
        var ledger = KnownIssueLedger.Create(records);
        writer.Write(ledger);
    }

    private static KnownIssueRecord Build(
        string id,
        KnownIssueSeverity severity,
        KnownIssueStatus status = KnownIssueStatus.Open) => new(
        Id: id,
        Severity: severity,
        Status: status,
        Summary: $"summary for {id}",
        Detail: null,
        ObservedAtUtc: new DateTimeOffset(2026, 5, 28, 12, 0, 0, TimeSpan.Zero));

    private sealed class CapturingLog : ILog
    {
        public List<string> Errors { get; } = new();

        public List<string> Warnings { get; } = new();

        public bool IsEnabled(LogLevel level) => true;

        public void Log(LogLevel level, string message, Exception? exception = null)
        {
            if (level == LogLevel.Error)
            {
                Errors.Add(message);
            }
            else if (level == LogLevel.Warning)
            {
                Warnings.Add(message);
            }
        }
    }
}
