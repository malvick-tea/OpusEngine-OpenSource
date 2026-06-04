using System;
using System.Linq;
using FluentAssertions;
using Opus.Engine.AlphaStress.KnownIssues;
using Xunit;

namespace Opus.Engine.AlphaStress.Tests.KnownIssues;

public sealed class KnownIssueLedgerMergerTests
{
    [Fact]
    public void Merge_rejects_null_inputs()
    {
        var act = () => KnownIssueLedgerMerger.Merge(null!, KnownIssueLedger.Empty);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Merge_two_empty_ledgers_yields_empty_ledger()
    {
        var merged = KnownIssueLedgerMerger.Merge(KnownIssueLedger.Empty, KnownIssueLedger.Empty);

        merged.TotalCount.Should().Be(0);
    }

    [Fact]
    public void Merge_unions_disjoint_records()
    {
        var baseLedger = KnownIssueLedger.Create(new[]
        {
            Build("base-1", KnownIssueSeverity.Blocker),
        });
        var overlay = KnownIssueLedger.Create(new[]
        {
            Build("overlay-1", KnownIssueSeverity.MustFix),
        });

        var merged = KnownIssueLedgerMerger.Merge(baseLedger, overlay);

        merged.Records.Select(r => r.Id).Should().BeEquivalentTo(new[] { "base-1", "overlay-1" });
    }

    [Fact]
    public void Overlay_record_wins_on_id_collision()
    {
        var baseLedger = KnownIssueLedger.Create(new[]
        {
            Build("shared", KnownIssueSeverity.Blocker, KnownIssueStatus.Open),
        });
        var overlay = KnownIssueLedger.Create(new[]
        {
            Build("shared", KnownIssueSeverity.MustFix, KnownIssueStatus.Closed),
        });

        var merged = KnownIssueLedgerMerger.Merge(baseLedger, overlay);

        merged.Records.Should().ContainSingle();
        merged.Records[0].Severity.Should().Be(KnownIssueSeverity.MustFix);
        merged.Records[0].Status.Should().Be(KnownIssueStatus.Closed);
    }

    [Fact]
    public void Merged_ledger_is_re_ordered_by_severity_then_id()
    {
        var baseLedger = KnownIssueLedger.Create(new[]
        {
            Build("z-base", KnownIssueSeverity.PostAlpha),
            Build("a-base", KnownIssueSeverity.MustFix),
        });
        var overlay = KnownIssueLedger.Create(new[]
        {
            Build("a-overlay", KnownIssueSeverity.Blocker),
        });

        var merged = KnownIssueLedgerMerger.Merge(baseLedger, overlay);

        merged.Records.Select(r => r.Id).Should().Equal("a-overlay", "a-base", "z-base");
    }

    [Fact]
    public void Merged_ledger_re_computes_open_counts()
    {
        var baseLedger = KnownIssueLedger.Create(new[]
        {
            Build("blk", KnownIssueSeverity.Blocker, KnownIssueStatus.Open),
        });
        var overlay = KnownIssueLedger.Create(new[]
        {
            Build("blk", KnownIssueSeverity.Blocker, KnownIssueStatus.Closed),
        });

        var merged = KnownIssueLedgerMerger.Merge(baseLedger, overlay);

        merged.OpenBlockerCount.Should().Be(0);
        merged.TotalCount.Should().Be(1);
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
}
