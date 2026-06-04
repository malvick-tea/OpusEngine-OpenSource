using System;

namespace Opus.Engine.AlphaStress.Stress;

/// <summary>
/// One issue observed during a stress run. The harness emits these into
/// <see cref="AlphaStressOutcome.Issues"/> after every iteration that has something
/// worth surfacing to a tester. Each issue carries the stable diagnostic code, a free-
/// form invariant message, the optional iteration index it relates to, and the UTC
/// timestamp it was recorded at.
/// </summary>
/// <param name="Code">Strongly-typed issue kind.</param>
/// <param name="DiagnosticCode">Stable <c>OPDX-STR-*</c> code derived from
/// <paramref name="Code"/>.</param>
/// <param name="Message">Invariant message suitable for log lines.</param>
/// <param name="IterationIndex">Optional zero-based iteration index the issue relates
/// to. Null for run-wide aggregated issues (framing, memory, ledger).</param>
/// <param name="ObservedAtUtc">UTC timestamp the harness recorded the issue at.</param>
public sealed record AlphaStressIssue(
    AlphaStressIssueCode Code,
    string DiagnosticCode,
    string Message,
    int? IterationIndex,
    DateTimeOffset ObservedAtUtc)
{
    /// <summary>Builds an iteration-scoped issue with the supplied code, message, and
    /// iteration index. Normalises the timestamp to UTC and resolves the diagnostic
    /// code through <see cref="AlphaStressIssueCodeStrings.ToDiagnosticCode"/>.</summary>
    public static AlphaStressIssue ForIteration(
        AlphaStressIssueCode code,
        int iterationIndex,
        string message,
        DateTimeOffset observedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (iterationIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(iterationIndex), "iterationIndex must be non-negative.");
        }

        return new AlphaStressIssue(
            Code: code,
            DiagnosticCode: AlphaStressIssueCodeStrings.ToDiagnosticCode(code),
            Message: message,
            IterationIndex: iterationIndex,
            ObservedAtUtc: observedAtUtc.ToUniversalTime());
    }

    /// <summary>Builds a run-wide aggregated issue with no specific iteration scope.</summary>
    public static AlphaStressIssue Global(
        AlphaStressIssueCode code,
        string message,
        DateTimeOffset observedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new AlphaStressIssue(
            Code: code,
            DiagnosticCode: AlphaStressIssueCodeStrings.ToDiagnosticCode(code),
            Message: message,
            IterationIndex: null,
            ObservedAtUtc: observedAtUtc.ToUniversalTime());
    }
}
