using System;
using FluentAssertions;
using Opus.Engine.AlphaStress.KnownIssues;
using Xunit;

namespace Opus.Engine.AlphaStress.Tests.KnownIssues;

public sealed class KnownIssueRecordTests
{
    [Fact]
    public void Validate_accepts_canonical_record()
    {
        var record = new KnownIssueRecord(
            Id: "STR-2026-001",
            Severity: KnownIssueSeverity.Blocker,
            Status: KnownIssueStatus.Open,
            Summary: "Alpha host crashes on resize",
            Detail: "Repro steps live in tester ticket #42",
            ObservedAtUtc: DateTimeOffset.UtcNow);

        var act = record.Validate;

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_rejects_empty_id(string id)
    {
        var record = new KnownIssueRecord(
            Id: id,
            Severity: KnownIssueSeverity.Blocker,
            Status: KnownIssueStatus.Open,
            Summary: "valid",
            Detail: null,
            ObservedAtUtc: DateTimeOffset.UtcNow);

        var act = record.Validate;

        act.Should().Throw<ArgumentException>().WithParameterName("Id");
    }

    [Fact]
    public void Validate_rejects_id_above_max_length()
    {
        var record = new KnownIssueRecord(
            Id: new string('x', KnownIssueRecord.MaxIdLength + 1),
            Severity: KnownIssueSeverity.MustFix,
            Status: KnownIssueStatus.Open,
            Summary: "valid",
            Detail: null,
            ObservedAtUtc: DateTimeOffset.UtcNow);

        var act = record.Validate;

        act.Should().Throw<ArgumentException>().WithParameterName("Id");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_rejects_empty_summary(string summary)
    {
        var record = new KnownIssueRecord(
            Id: "valid-id",
            Severity: KnownIssueSeverity.Blocker,
            Status: KnownIssueStatus.Open,
            Summary: summary,
            Detail: null,
            ObservedAtUtc: DateTimeOffset.UtcNow);

        var act = record.Validate;

        act.Should().Throw<ArgumentException>().WithParameterName("Summary");
    }

    [Fact]
    public void Validate_rejects_summary_above_max_length()
    {
        var record = new KnownIssueRecord(
            Id: "valid-id",
            Severity: KnownIssueSeverity.Blocker,
            Status: KnownIssueStatus.Open,
            Summary: new string('s', KnownIssueRecord.MaxSummaryLength + 1),
            Detail: null,
            ObservedAtUtc: DateTimeOffset.UtcNow);

        var act = record.Validate;

        act.Should().Throw<ArgumentException>().WithParameterName("Summary");
    }

    [Fact]
    public void Normalised_trims_id_summary_and_normalises_detail()
    {
        var record = new KnownIssueRecord(
            Id: "  trim-me  ",
            Severity: KnownIssueSeverity.MustFix,
            Status: KnownIssueStatus.Closed,
            Summary: "  alpha host hitch  ",
            Detail: "   ",
            ObservedAtUtc: new DateTimeOffset(2026, 7, 14, 10, 0, 0, TimeSpan.FromHours(2)));

        var normalised = record.Normalised();

        normalised.Id.Should().Be("trim-me");
        normalised.Summary.Should().Be("alpha host hitch");
        normalised.Detail.Should().BeNull();
        normalised.ObservedAtUtc.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Normalised_preserves_non_blank_detail()
    {
        var record = new KnownIssueRecord(
            Id: "id",
            Severity: KnownIssueSeverity.PostAlpha,
            Status: KnownIssueStatus.Open,
            Summary: "summary",
            Detail: "  multi  \n line  ",
            ObservedAtUtc: DateTimeOffset.UtcNow);

        var normalised = record.Normalised();

        normalised.Detail.Should().Be("multi  \n line");
    }
}
