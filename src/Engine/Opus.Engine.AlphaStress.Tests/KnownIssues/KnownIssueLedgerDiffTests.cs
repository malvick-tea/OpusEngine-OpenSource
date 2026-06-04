using System;
using System.Linq;
using FluentAssertions;
using Opus.Engine.AlphaStress.KnownIssues;
using Xunit;

namespace Opus.Engine.AlphaStress.Tests.KnownIssues;

public sealed class KnownIssueLedgerDiffTests
{
    [Fact]
    public void Compute_two_empty_ledgers_yields_empty_diff()
    {
        var diff = KnownIssueLedgerDiff.Compute(KnownIssueLedger.Empty, KnownIssueLedger.Empty);

        diff.Changes.Should().BeEmpty();
        diff.HasChanges.Should().BeFalse();
        diff.AddedCount.Should().Be(0);
        diff.RemovedCount.Should().Be(0);
        diff.ChangedCount.Should().Be(0);
        diff.UnchangedCount.Should().Be(0);
    }

    [Fact]
    public void Compute_classifies_records_by_id_membership()
    {
        var left = KnownIssueLedger.Create(new[]
        {
            Build("only-left", KnownIssueSeverity.MustFix),
            Build("both-unchanged", KnownIssueSeverity.Blocker),
            Build("both-changed", KnownIssueSeverity.Blocker),
        });
        var right = KnownIssueLedger.Create(new[]
        {
            Build("only-right", KnownIssueSeverity.PostAlpha),
            Build("both-unchanged", KnownIssueSeverity.Blocker),
            Build("both-changed", KnownIssueSeverity.MustFix),
        });

        var diff = KnownIssueLedgerDiff.Compute(left, right);

        diff.AddedCount.Should().Be(1);
        diff.RemovedCount.Should().Be(1);
        diff.ChangedCount.Should().Be(1);
        diff.UnchangedCount.Should().Be(1);
        diff.HasChanges.Should().BeTrue();
    }

    [Fact]
    public void Compute_orders_changes_by_kind_then_id_ordinal()
    {
        var left = KnownIssueLedger.Create(new[]
        {
            Build("z-changed", KnownIssueSeverity.Blocker),
            Build("a-removed", KnownIssueSeverity.MustFix),
        });
        var right = KnownIssueLedger.Create(new[]
        {
            Build("z-changed", KnownIssueSeverity.MustFix),
            Build("a-added", KnownIssueSeverity.PostAlpha),
        });

        var diff = KnownIssueLedgerDiff.Compute(left, right);

        diff.Changes.Select(c => c.Kind).Should().Equal(
            KnownIssueChangeKind.Added,
            KnownIssueChangeKind.Removed,
            KnownIssueChangeKind.Changed);
        diff.Changes[0].Id.Should().Be("a-added");
        diff.Changes[1].Id.Should().Be("a-removed");
        diff.Changes[2].Id.Should().Be("z-changed");
    }

    [Fact]
    public void Compute_detail_field_changes_classify_as_changed()
    {
        var left = KnownIssueLedger.Create(new[]
        {
            BuildWithDetail("id-1", "old detail body"),
        });
        var right = KnownIssueLedger.Create(new[]
        {
            BuildWithDetail("id-1", "new detail body"),
        });

        var diff = KnownIssueLedgerDiff.Compute(left, right);

        diff.ChangedCount.Should().Be(1);
        diff.Changes[0].Left!.Detail.Should().Be("old detail body");
        diff.Changes[0].Right!.Detail.Should().Be("new detail body");
    }

    [Fact]
    public void Compute_rejects_null_ledgers()
    {
        var act = () => KnownIssueLedgerDiff.Compute(null!, KnownIssueLedger.Empty);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Removed_records_carry_left_payload_with_null_right()
    {
        var left = KnownIssueLedger.Create(new[]
        {
            Build("gone", KnownIssueSeverity.Blocker),
        });

        var diff = KnownIssueLedgerDiff.Compute(left, KnownIssueLedger.Empty);

        diff.Changes.Should().ContainSingle();
        var change = diff.Changes[0];
        change.Kind.Should().Be(KnownIssueChangeKind.Removed);
        change.Right.Should().BeNull();
        change.Left!.Id.Should().Be("gone");
    }

    [Fact]
    public void Added_records_carry_right_payload_with_null_left()
    {
        var right = KnownIssueLedger.Create(new[]
        {
            Build("fresh", KnownIssueSeverity.MustFix),
        });

        var diff = KnownIssueLedgerDiff.Compute(KnownIssueLedger.Empty, right);

        diff.Changes.Should().ContainSingle();
        var change = diff.Changes[0];
        change.Kind.Should().Be(KnownIssueChangeKind.Added);
        change.Left.Should().BeNull();
        change.Right!.Id.Should().Be("fresh");
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

    private static KnownIssueRecord BuildWithDetail(string id, string detail) => new(
        Id: id,
        Severity: KnownIssueSeverity.MustFix,
        Status: KnownIssueStatus.Open,
        Summary: $"summary for {id}",
        Detail: detail,
        ObservedAtUtc: new DateTimeOffset(2026, 5, 28, 12, 0, 0, TimeSpan.Zero));
}
