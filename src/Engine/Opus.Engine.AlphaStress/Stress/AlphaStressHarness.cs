using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Opus.Engine.AlphaHarness.Smoke;
using Opus.Engine.AlphaStress.FramePacing;
using Opus.Engine.AlphaStress.KnownIssues;
using Opus.Engine.AlphaStress.Memory;
using Opus.Engine.AlphaStress.Network;

namespace Opus.Engine.AlphaStress.Stress;

/// <summary>
/// Pure orchestrator for an alpha stress run. Walks the configured iteration count,
/// drives an <see cref="IAlphaStressIterationRunner"/> per iteration, collects frame
/// pacing observations into per-iteration and aggregated aggregators, captures memory
/// samples around the entire run, and folds the configured thresholds + ledger state
/// into a deterministic <see cref="AlphaStressOutcome"/>. Owns no state of its own
/// across calls — the harness is a one-shot orchestrator.
/// </summary>
public static class AlphaStressHarness
{
    /// <summary>Runs the workload described by <paramref name="profile"/> against
    /// <paramref name="iterationRunner"/>, with an optional <paramref name="memoryProbe"/>,
    /// optional <paramref name="networkProbe"/>, and optional <paramref name="knownIssues"/>
    /// ledger. The network probe is exercised only when <see cref="AlphaStressProfile.Network"/>
    /// is non-null; when the profile asks for network injection but the caller does not
    /// supply a probe, the harness auto-constructs a <see cref="LoopbackFaultInjectionNetworkProbe"/>
    /// and disposes it before returning.</summary>
    public static AlphaStressOutcome Run(
        AlphaStressProfile profile,
        IAlphaStressIterationRunner iterationRunner,
        IMemoryProbe? memoryProbe = null,
        IAlphaStressNetworkProbe? networkProbe = null,
        KnownIssueLedger? knownIssues = null,
        TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(iterationRunner);
        profile.Validate();
        var clock = time ?? TimeProvider.System;
        var probe = memoryProbe ?? new SystemMemoryProbe(clock);
        var ledger = knownIssues ?? KnownIssueLedger.Empty;
        var runStartedAtUtc = clock.GetUtcNow();
        var stopwatch = Stopwatch.StartNew();

        var (effectiveNetworkProbe, ownsNetworkProbe) = ResolveNetworkProbe(profile, networkProbe, clock);
        try
        {
            var aggregatedFramePacing = new FramePacingAggregator(profile.FramePacing.HitchThreshold);
            var memoryAggregator = new MemoryProbeAggregator();
            var networkAggregator = new AlphaStressNetworkAggregator();
            var iterations = new List<AlphaStressIterationOutcome>(profile.IterationCount);
            var issues = new List<AlphaStressIssue>();

            memoryAggregator.Record(probe.Capture());

            for (var iterationIndex = 0; iterationIndex < profile.IterationCount; iterationIndex++)
            {
                if (stopwatch.Elapsed > profile.WallClockBudget)
                {
                    issues.Add(AlphaStressIssue.Global(
                        AlphaStressIssueCode.BudgetExceeded,
                        BudgetMessage(profile, iterationIndex),
                        clock.GetUtcNow()));
                    break;
                }

                var iterationOutcome = RunIteration(profile, iterationIndex, iterationRunner, aggregatedFramePacing, clock);
                iterations.Add(iterationOutcome);
                AppendIterationIssues(iterationOutcome, issues, clock);
                RecordNetworkObservation(profile, iterationIndex, effectiveNetworkProbe, networkAggregator);
                memoryAggregator.Record(probe.Capture());
            }

            var framePacingSummary = aggregatedFramePacing.BuildSummary();
            var memorySummary = memoryAggregator.BuildSummary();
            var networkSummary = networkAggregator.BuildSummary();
            AppendFramePacingIssue(profile, framePacingSummary, issues, clock);
            AppendMemoryIssue(profile, memorySummary, issues, clock);
            AppendNetworkIssue(profile, networkSummary, issues, clock);
            AppendLedgerIssues(ledger, issues, clock);

            stopwatch.Stop();
            return AlphaStressOutcome.Create(
                profile: profile,
                startedAtUtc: runStartedAtUtc,
                elapsedWallClock: stopwatch.Elapsed,
                iterations: iterations,
                aggregatedFramePacing: framePacingSummary,
                memoryProbe: memorySummary,
                network: networkSummary,
                issues: issues);
        }
        finally
        {
            if (ownsNetworkProbe)
            {
                effectiveNetworkProbe?.Dispose();
            }
        }
    }

    private static (IAlphaStressNetworkProbe? Probe, bool OwnsProbe) ResolveNetworkProbe(
        AlphaStressProfile profile,
        IAlphaStressNetworkProbe? caller,
        TimeProvider clock)
    {
        if (profile.Network is null)
        {
            return (caller, OwnsProbe: false);
        }

        if (caller is not null)
        {
            return (caller, OwnsProbe: false);
        }

        return (new LoopbackFaultInjectionNetworkProbe(clock), OwnsProbe: true);
    }

    private static void RecordNetworkObservation(
        AlphaStressProfile profile,
        int iterationIndex,
        IAlphaStressNetworkProbe? networkProbe,
        AlphaStressNetworkAggregator aggregator)
    {
        if (profile.Network is null || networkProbe is null)
        {
            return;
        }

        var observation = networkProbe.RunIteration(iterationIndex, profile.Network);
        aggregator.Record(observation);
    }

    private static void AppendNetworkIssue(
        AlphaStressProfile profile,
        AlphaStressNetworkSummary summary,
        List<AlphaStressIssue> issues,
        TimeProvider clock)
    {
        if (profile.Network is null || !summary.HasObservations)
        {
            return;
        }

        var tolerance = profile.Network.Tolerance;
        if (summary.DropFraction <= tolerance.MaxDropRate
            && summary.InboundDropFraction <= tolerance.MaxInboundDropRate
            && summary.TotalSoakIssueCount <= tolerance.MaxObservedSoakIssues)
        {
            return;
        }

        issues.Add(AlphaStressIssue.Global(
            AlphaStressIssueCode.FaultInjectionDegraded,
            NetworkMessage(profile, summary),
            clock.GetUtcNow()));
    }

    private static AlphaStressIterationOutcome RunIteration(
        AlphaStressProfile profile,
        int iterationIndex,
        IAlphaStressIterationRunner iterationRunner,
        FramePacingAggregator aggregated,
        TimeProvider clock)
    {
        var iterationStartedAtUtc = clock.GetUtcNow();
        var iterationStopwatch = Stopwatch.StartNew();
        AlphaStressIterationRunResult result;
        try
        {
            result = iterationRunner.Run(iterationIndex, profile.IterationProfile);
        }
        catch (Exception ex)
        {
            iterationStopwatch.Stop();
            return new AlphaStressIterationOutcome(
                IterationIndex: iterationIndex,
                StartedAtUtc: iterationStartedAtUtc,
                ElapsedWallClock: iterationStopwatch.Elapsed,
                SmokeOutcome: null,
                FramePacing: FramePacingSummary.Empty(profile.FramePacing.HitchThreshold),
                UnhandledExceptionMessage: BuildExceptionMessage(ex));
        }

        iterationStopwatch.Stop();
        var perIteration = new FramePacingAggregator(profile.FramePacing.HitchThreshold);
        var nextGlobalFrame = (long)aggregated.SampleCount + 1;
        foreach (var observation in result.FramePacingObservations)
        {
            perIteration.Record(observation);
            aggregated.Record(observation with { FrameNumber = nextGlobalFrame });
            nextGlobalFrame++;
        }

        return new AlphaStressIterationOutcome(
            IterationIndex: iterationIndex,
            StartedAtUtc: iterationStartedAtUtc,
            ElapsedWallClock: iterationStopwatch.Elapsed,
            SmokeOutcome: result.SmokeOutcome,
            FramePacing: perIteration.BuildSummary(),
            UnhandledExceptionMessage: result.UnhandledException is null ? null : BuildExceptionMessage(result.UnhandledException));
    }

    private static void AppendIterationIssues(
        AlphaStressIterationOutcome iteration,
        List<AlphaStressIssue> issues,
        TimeProvider clock)
    {
        if (iteration.UnhandledExceptionMessage is not null)
        {
            issues.Add(AlphaStressIssue.ForIteration(
                AlphaStressIssueCode.IterationUnhandledException,
                iteration.IterationIndex,
                iteration.UnhandledExceptionMessage,
                clock.GetUtcNow()));
            return;
        }

        if (iteration.SmokeOutcome is null)
        {
            issues.Add(AlphaStressIssue.ForIteration(
                AlphaStressIssueCode.HostUnavailable,
                iteration.IterationIndex,
                IterationHostUnavailableMessage(iteration.IterationIndex),
                clock.GetUtcNow()));
            return;
        }

        if (!iteration.SmokeOutcome.IsClean)
        {
            issues.Add(AlphaStressIssue.ForIteration(
                AlphaStressIssueCode.IterationFailed,
                iteration.IterationIndex,
                IterationFailedMessage(iteration),
                clock.GetUtcNow()));
        }
    }

    private static void AppendFramePacingIssue(
        AlphaStressProfile profile,
        FramePacingSummary summary,
        List<AlphaStressIssue> issues,
        TimeProvider clock)
    {
        if (!summary.HasSamples)
        {
            return;
        }

        if (summary.Percentile95 <= profile.FramePacing.P95Limit
            && summary.Percentile99 <= profile.FramePacing.P99Limit
            && summary.Max <= profile.FramePacing.MaxLimit
            && summary.HitchCount <= profile.FramePacing.HitchCountLimit)
        {
            return;
        }

        issues.Add(AlphaStressIssue.Global(
            AlphaStressIssueCode.FramePacingDegraded,
            FramePacingMessage(profile, summary),
            clock.GetUtcNow()));
    }

    private static void AppendMemoryIssue(
        AlphaStressProfile profile,
        MemoryProbeSummary summary,
        List<AlphaStressIssue> issues,
        TimeProvider clock)
    {
        if (!summary.HasSamples)
        {
            return;
        }

        if (summary.ManagedHeapGrowthBytes <= profile.Memory.ManagedHeapGrowthLimitBytes
            && summary.WorkingSetGrowthBytes <= profile.Memory.WorkingSetGrowthLimitBytes
            && summary.Gen2CollectionsDelta <= profile.Memory.Gen2CollectionLimit)
        {
            return;
        }

        issues.Add(AlphaStressIssue.Global(
            AlphaStressIssueCode.MemoryGrowthExceeded,
            MemoryMessage(profile, summary),
            clock.GetUtcNow()));
    }

    private static void AppendLedgerIssues(
        KnownIssueLedger ledger,
        List<AlphaStressIssue> issues,
        TimeProvider clock)
    {
        if (ledger.OpenBlockerCount > 0)
        {
            issues.Add(AlphaStressIssue.Global(
                AlphaStressIssueCode.KnownIssueBlockerOpen,
                LedgerMessage("blocker", ledger.OpenBlockerCount),
                clock.GetUtcNow()));
        }

        if (ledger.OpenMustFixCount > 0)
        {
            issues.Add(AlphaStressIssue.Global(
                AlphaStressIssueCode.KnownIssueMustFixOpen,
                LedgerMessage("must-fix", ledger.OpenMustFixCount),
                clock.GetUtcNow()));
        }
    }

    private static string IterationHostUnavailableMessage(int iterationIndex) =>
        string.Create(CultureInfo.InvariantCulture, $"iteration {iterationIndex} could not open the alpha host");

    private static string IterationFailedMessage(AlphaStressIterationOutcome iteration) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"iteration {iteration.IterationIndex} produced {iteration.SmokeOutcome!.Issues.Count} smoke issue(s)");

    private static string BudgetMessage(AlphaStressProfile profile, int iterationIndex) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"wall-clock budget {profile.WallClockBudget.TotalMilliseconds:F0}ms exhausted before iteration {iterationIndex} could start");

    private static string FramePacingMessage(AlphaStressProfile profile, FramePacingSummary summary) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"frame pacing degraded: p95={summary.Percentile95.TotalMilliseconds:F2}ms (limit {profile.FramePacing.P95Limit.TotalMilliseconds:F2}ms), p99={summary.Percentile99.TotalMilliseconds:F2}ms, max={summary.Max.TotalMilliseconds:F2}ms, hitches={summary.HitchCount} (limit {profile.FramePacing.HitchCountLimit})");

    private static string MemoryMessage(AlphaStressProfile profile, MemoryProbeSummary summary) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"memory growth exceeded: managed +{summary.ManagedHeapGrowthBytes / 1024} KiB (limit {profile.Memory.ManagedHeapGrowthLimitBytes / 1024} KiB), workingSet +{summary.WorkingSetGrowthBytes / 1024} KiB (limit {profile.Memory.WorkingSetGrowthLimitBytes / 1024} KiB), gen2Delta={summary.Gen2CollectionsDelta} (limit {profile.Memory.Gen2CollectionLimit})");

    private static string NetworkMessage(AlphaStressProfile profile, AlphaStressNetworkSummary summary) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"fault-injection degraded: outDrops={summary.TotalDroppedPackets}/{summary.TotalClientSendAttempts} (frac {summary.DropFraction:F3}, limit {profile.Network!.Tolerance.MaxDropRate:F3}), inDrops={summary.TotalInboundDroppedPackets}/{summary.TotalInboundAttempts} (frac {summary.InboundDropFraction:F3}, limit {profile.Network.Tolerance.MaxInboundDropRate:F3}), outDelayed={summary.TotalDelayedPackets}, inDelayed={summary.TotalInboundDelayedPackets}, soakIssues={summary.TotalSoakIssueCount} (limit {profile.Network.Tolerance.MaxObservedSoakIssues})");

    private static string LedgerMessage(string label, int count) =>
        string.Create(CultureInfo.InvariantCulture, $"known-issue ledger has {count} open {label} entr{(count == 1 ? "y" : "ies")}");

    private static string BuildExceptionMessage(Exception ex) =>
        string.Create(CultureInfo.InvariantCulture, $"{ex.GetType().FullName}: {ex.Message}");
}
