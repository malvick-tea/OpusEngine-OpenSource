using System;
using FluentAssertions;
using Opus.Engine.AlphaStress.Stress;
using Xunit;

namespace Opus.Engine.AlphaStress.Tests.Stress;

public sealed class AlphaStressIssueTests
{
    [Fact]
    public void ForIteration_resolves_diagnostic_code()
    {
        var issue = AlphaStressIssue.ForIteration(
            AlphaStressIssueCode.IterationFailed,
            iterationIndex: 3,
            message: "iteration 3 produced 2 smoke issue(s)",
            observedAtUtc: DateTimeOffset.UtcNow);

        issue.DiagnosticCode.Should().Be(AlphaStressDiagnosticCodes.StressIterationFailed);
        issue.IterationIndex.Should().Be(3);
        issue.Message.Should().Contain("iteration 3");
        issue.ObservedAtUtc.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Global_resolves_diagnostic_code_with_null_iteration()
    {
        var issue = AlphaStressIssue.Global(
            AlphaStressIssueCode.BudgetExceeded,
            message: "budget exceeded",
            observedAtUtc: DateTimeOffset.UtcNow);

        issue.DiagnosticCode.Should().Be(AlphaStressDiagnosticCodes.StressBudgetExceeded);
        issue.IterationIndex.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ForIteration_rejects_empty_message(string message)
    {
        var act = () => AlphaStressIssue.ForIteration(
            AlphaStressIssueCode.IterationFailed,
            iterationIndex: 0,
            message: message,
            observedAtUtc: DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ForIteration_rejects_negative_iteration_index()
    {
        var act = () => AlphaStressIssue.ForIteration(
            AlphaStressIssueCode.IterationFailed,
            iterationIndex: -1,
            message: "iteration -1",
            observedAtUtc: DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Global_rejects_empty_message()
    {
        var act = () => AlphaStressIssue.Global(
            AlphaStressIssueCode.BudgetExceeded,
            message: string.Empty,
            observedAtUtc: DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }
}
