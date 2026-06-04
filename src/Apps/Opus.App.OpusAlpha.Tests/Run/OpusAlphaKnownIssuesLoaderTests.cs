using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Opus.App.OpusAlpha.Run;
using Opus.Engine.AlphaStress.KnownIssues;
using Opus.Foundation;
using Xunit;

namespace Opus.App.OpusAlpha.Tests.Run;

public sealed class OpusAlphaKnownIssuesLoaderTests : IDisposable
{
    private readonly string _root;

    public OpusAlphaKnownIssuesLoaderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "opus-alpha-ki-load-" + Guid.NewGuid().ToString("N"));
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
    public void Load_null_path_returns_empty_ledger()
    {
        var log = new CapturingLog();

        var ledger = OpusAlphaKnownIssuesLoader.Load(path: null, log);

        ledger.Should().BeSameAs(KnownIssueLedger.Empty);
        log.Warnings.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Load_blank_path_returns_empty_ledger(string path)
    {
        var log = new CapturingLog();

        var ledger = OpusAlphaKnownIssuesLoader.Load(path, log);

        ledger.Should().BeSameAs(KnownIssueLedger.Empty);
    }

    [Fact]
    public void Load_missing_file_warns_and_returns_empty()
    {
        var log = new CapturingLog();
        var path = Path.Combine(_root, "does-not-exist.json");

        var ledger = OpusAlphaKnownIssuesLoader.Load(path, log);

        ledger.Should().BeSameAs(KnownIssueLedger.Empty);
        log.Warnings.Should().ContainSingle().Which.Should().Contain("was not found");
    }

    [Fact]
    public void Load_round_trips_ledger_through_writer()
    {
        var path = Path.Combine(_root, "ki.json");
        var writer = new KnownIssueLedgerWriter(new KnownIssueLedgerWriterOptions(path));
        var ledger = KnownIssueLedger.Create(new[]
        {
            new KnownIssueRecord("blk-1", KnownIssueSeverity.Blocker, KnownIssueStatus.Open, "blocker summary", null, DateTimeOffset.UtcNow),
            new KnownIssueRecord("mst-1", KnownIssueSeverity.MustFix, KnownIssueStatus.Closed, "fixed summary", null, DateTimeOffset.UtcNow),
        });
        writer.Write(ledger);
        var log = new CapturingLog();

        var loaded = OpusAlphaKnownIssuesLoader.Load(path, log);

        loaded.TotalCount.Should().Be(2);
        loaded.OpenBlockerCount.Should().Be(1);
        log.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Load_malformed_json_warns_and_returns_empty()
    {
        var path = Path.Combine(_root, "broken.json");
        File.WriteAllText(path, "{ not json");
        var log = new CapturingLog();

        var ledger = OpusAlphaKnownIssuesLoader.Load(path, log);

        ledger.Should().BeSameAs(KnownIssueLedger.Empty);
        log.Warnings.Should().ContainSingle().Which.Should().Contain("failed to parse");
    }

    [Fact]
    public void Load_invalid_payload_warns_and_returns_empty()
    {
        var path = Path.Combine(_root, "invalid.json");
        File.WriteAllText(path, "[{\"id\":\"\",\"severity\":\"Blocker\",\"status\":\"Open\",\"summary\":\"\",\"detail\":null,\"observedAtUtc\":\"2026-07-14T00:00:00+00:00\"}]");
        var log = new CapturingLog();

        var ledger = OpusAlphaKnownIssuesLoader.Load(path, log);

        ledger.Should().BeSameAs(KnownIssueLedger.Empty);
        log.Warnings.Should().ContainSingle().Which.Should().Contain("failed validation");
    }

    private sealed class CapturingLog : ILog
    {
        public List<string> Warnings { get; } = new();

        public bool IsEnabled(LogLevel level) => true;

        public void Log(LogLevel level, string message, Exception? exception = null)
        {
            if (level == LogLevel.Warning)
            {
                Warnings.Add(message);
            }
        }
    }
}
