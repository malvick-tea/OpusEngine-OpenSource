using System;
using System.Linq;
using FluentAssertions;
using Opus.Engine.AlphaStress.KnownIssues;
using Xunit;

namespace Opus.Engine.AlphaStress.Tests.KnownIssues;

public sealed class KnownIssueLedgerTests
{
    [Fact]
    public void Empty_ledger_reports_zero_counts()
    {
        var ledger = KnownIssueLedger.Empty;

        ledger.TotalCount.Should().Be(0);
        ledger.OpenBlockerCount.Should().Be(0);
        ledger.OpenMustFixCount.Should().Be(0);
        ledger.OpenPostAlphaCount.Should().Be(0);
        ledger.Records.Should().BeEmpty();
    }

    [Fact]
    public void Create_null_throws()
    {
        var act = () => KnownIssueLedger.Create(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_null_entry_throws()
    {
        var records = new KnownIssueRecord?[] { null };

        var act = () => KnownIssueLedger.Create(records!);

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("records");
    }

    [Fact]
    public void Create_duplicate_id_throws()
    {
        var first = Build("dup", KnownIssueSeverity.Blocker);
        var second = Build("dup", KnownIssueSeverity.PostAlpha);

        var act = () => KnownIssueLedger.Create(new[] { first, second });

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("records");
    }

    [Fact]
    public void Create_counts_open_blockers_must_fix_and_post_alpha()
    {
        var records = new[]
        {
            Build("blk-1", KnownIssueSeverity.Blocker),
            Build("blk-2", KnownIssueSeverity.Blocker, KnownIssueStatus.Closed),
            Build("mst-1", KnownIssueSeverity.MustFix),
            Build("mst-2", KnownIssueSeverity.MustFix),
            Build("psa-1", KnownIssueSeverity.PostAlpha),
        };

        var ledger = KnownIssueLedger.Create(records);

        ledger.OpenBlockerCount.Should().Be(1);
        ledger.OpenMustFixCount.Should().Be(2);
        ledger.OpenPostAlphaCount.Should().Be(1);
        ledger.TotalCount.Should().Be(5);
    }

    [Fact]
    public void Create_orders_records_by_severity_then_id()
    {
        var records = new[]
        {
            Build("z-blk", KnownIssueSeverity.Blocker),
            Build("a-mst", KnownIssueSeverity.MustFix),
            Build("a-blk", KnownIssueSeverity.Blocker),
        };

        var ledger = KnownIssueLedger.Create(records);

        ledger.Records.Select(r => r.Id).Should().Equal("a-blk", "z-blk", "a-mst");
    }

    [Fact]
    public void WithSeverity_returns_only_matching_records()
    {
        var ledger = KnownIssueLedger.Create(new[]
        {
            Build("blk-1", KnownIssueSeverity.Blocker),
            Build("mst-1", KnownIssueSeverity.MustFix),
        });

        ledger.WithSeverity(KnownIssueSeverity.Blocker).Should().ContainSingle().Which.Id.Should().Be("blk-1");
    }

    [Fact]
    public void OpenRecords_excludes_closed_entries()
    {
        var ledger = KnownIssueLedger.Create(new[]
        {
            Build("open-1", KnownIssueSeverity.MustFix),
            Build("closed-1", KnownIssueSeverity.MustFix, KnownIssueStatus.Closed),
        });

        ledger.OpenRecords().Should().ContainSingle().Which.Id.Should().Be("open-1");
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
