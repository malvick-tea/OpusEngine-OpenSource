using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Opus.Engine.AlphaStress.KnownIssues;
using Xunit;

namespace Opus.Engine.AlphaStress.Tests.KnownIssues;

public sealed class KnownIssueLedgerWriterTests : IDisposable
{
    private static readonly JsonSerializerOptions DeserialiserOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _root;

    public KnownIssueLedgerWriterTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "opus-alpha-stress-ki-" + Guid.NewGuid().ToString("N"));
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
    public void Write_persists_json_and_round_trips_through_deserialiser()
    {
        var path = Path.Combine(_root, "known-issues.json");
        var writer = new KnownIssueLedgerWriter(new KnownIssueLedgerWriterOptions(path));
        var ledger = KnownIssueLedger.Create(new[]
        {
            Build("blk-1", KnownIssueSeverity.Blocker),
            Build("mst-1", KnownIssueSeverity.MustFix, KnownIssueStatus.Closed),
        });

        var result = writer.Write(ledger);

        result.IsSuccess.Should().BeTrue();
        result.Path.Should().Be(path);
        var json = File.ReadAllText(path);
        json.Should().Contain("\"id\": \"blk-1\"");
        json.Should().Contain("\"severity\": \"Blocker\"");
    }

    [Fact]
    public void Write_creates_missing_parent_directory()
    {
        var nested = Path.Combine(_root, "nested", "ledger.json");
        var writer = new KnownIssueLedgerWriter(new KnownIssueLedgerWriterOptions(nested));

        var result = writer.Write(KnownIssueLedger.Empty);

        result.IsSuccess.Should().BeTrue();
        File.Exists(nested).Should().BeTrue();
    }

    [Fact]
    public void Write_overwrites_existing_file_atomically()
    {
        var path = Path.Combine(_root, "ledger.json");
        var writer = new KnownIssueLedgerWriter(new KnownIssueLedgerWriterOptions(path));
        var first = KnownIssueLedger.Create(new[] { Build("first", KnownIssueSeverity.Blocker) });
        var second = KnownIssueLedger.Create(new[] { Build("second", KnownIssueSeverity.MustFix) });

        writer.Write(first).IsSuccess.Should().BeTrue();
        writer.Write(second).IsSuccess.Should().BeTrue();

        var json = File.ReadAllText(path);
        json.Should().Contain("second");
        json.Should().NotContain("first");
        File.Exists(path + ".tmp").Should().BeFalse();
    }

    [Fact]
    public void Write_null_ledger_throws()
    {
        var writer = new KnownIssueLedgerWriter(new KnownIssueLedgerWriterOptions(Path.Combine(_root, "x.json")));

        var act = () => writer.Write(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Write_invalid_options_returns_structured_issue()
    {
        var writer = new KnownIssueLedgerWriter(new KnownIssueLedgerWriterOptions(string.Empty));

        var result = writer.Write(KnownIssueLedger.Empty);

        result.IsSuccess.Should().BeFalse();
        result.Issue.Should().NotBeNull();
        result.Issue!.Code.Should().Be(AlphaStressDiagnosticCodes.KnownIssueLedgerWriteFailed);
    }

    [Fact]
    public void Ctor_null_options_throws()
    {
        var act = () => new KnownIssueLedgerWriter(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Persisted_json_round_trips_back_to_records()
    {
        var path = Path.Combine(_root, "ledger.json");
        var writer = new KnownIssueLedgerWriter(new KnownIssueLedgerWriterOptions(path));
        var ledger = KnownIssueLedger.Create(new[] { Build("blk-1", KnownIssueSeverity.Blocker) });
        writer.Write(ledger);

        var json = File.ReadAllText(path);
        var roundTripped = JsonSerializer.Deserialize<KnownIssueRecord[]>(json, DeserialiserOptions);

        roundTripped.Should().NotBeNull();
        roundTripped!.Should().HaveCount(1);
        roundTripped![0].Id.Should().Be("blk-1");
        roundTripped[0].Severity.Should().Be(KnownIssueSeverity.Blocker);
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
        ObservedAtUtc: new DateTimeOffset(2026, 7, 14, 12, 0, 0, TimeSpan.Zero));
}
