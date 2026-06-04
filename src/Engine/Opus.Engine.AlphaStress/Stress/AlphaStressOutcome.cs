using System;
using System.Collections.Generic;
using System.Linq;
using Opus.Engine.AlphaStress.FramePacing;
using Opus.Engine.AlphaStress.Memory;
using Opus.Engine.AlphaStress.Network;

namespace Opus.Engine.AlphaStress.Stress;

/// <summary>
/// Outcome of a complete stress run. Aggregates per-iteration outcomes, frame pacing,
/// memory probe summary, and every observed issue. <see cref="IsClean"/> mirrors the
/// existing M8 <c>NetSoakReport.IsClean</c> and M9 <c>AlphaSmokeOutcome.IsClean</c>
/// gates so a reporter already wired for those treats the stress run the same way.
/// </summary>
public sealed record AlphaStressOutcome(
    AlphaStressProfile Profile,
    DateTimeOffset StartedAtUtc,
    TimeSpan ElapsedWallClock,
    IReadOnlyList<AlphaStressIterationOutcome> Iterations,
    FramePacingSummary AggregatedFramePacing,
    MemoryProbeSummary MemoryProbe,
    AlphaStressNetworkSummary Network,
    IReadOnlyList<AlphaStressIssue> Issues)
{
    /// <summary>True when no issues were recorded.</summary>
    public bool IsClean => Issues.Count == 0;

    /// <summary>True when every iteration produced a clean smoke outcome.</summary>
    public bool AllIterationsClean => Iterations.All(static iteration => iteration.IsClean);

    /// <summary>Convenience accessor for issues with the supplied code; never null.</summary>
    public IEnumerable<AlphaStressIssue> IssuesWithCode(AlphaStressIssueCode code) =>
        Issues.Where(issue => issue.Code == code);

    /// <summary>Builder used by the harness; centralises validation so the stress
    /// pipeline never produces a half-shaped outcome.</summary>
    public static AlphaStressOutcome Create(
        AlphaStressProfile profile,
        DateTimeOffset startedAtUtc,
        TimeSpan elapsedWallClock,
        IEnumerable<AlphaStressIterationOutcome> iterations,
        FramePacingSummary aggregatedFramePacing,
        MemoryProbeSummary memoryProbe,
        AlphaStressNetworkSummary network,
        IEnumerable<AlphaStressIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(iterations);
        ArgumentNullException.ThrowIfNull(aggregatedFramePacing);
        ArgumentNullException.ThrowIfNull(memoryProbe);
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(issues);
        if (elapsedWallClock < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(elapsedWallClock),
                "elapsedWallClock must be non-negative.");
        }

        var orderedIterations = iterations
            .Where(static iteration => iteration is not null)
            .OrderBy(static iteration => iteration!.IterationIndex)
            .ToArray();
        var sortedIssues = issues
            .Where(static issue => issue is not null)
            .OrderBy(static issue => issue!.ObservedAtUtc)
            .ThenBy(static issue => issue!.DiagnosticCode, StringComparer.Ordinal)
            .ToArray();

        return new AlphaStressOutcome(
            Profile: profile,
            StartedAtUtc: startedAtUtc.ToUniversalTime(),
            ElapsedWallClock: elapsedWallClock,
            Iterations: orderedIterations,
            AggregatedFramePacing: aggregatedFramePacing,
            MemoryProbe: memoryProbe,
            Network: network,
            Issues: sortedIssues);
    }
}
