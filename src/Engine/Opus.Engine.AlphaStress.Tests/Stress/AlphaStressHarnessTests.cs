using System;
using System.Collections.Generic;
using FluentAssertions;
using Opus.Engine.AlphaHarness.Smoke;
using Opus.Engine.AlphaStress.FramePacing;
using Opus.Engine.AlphaStress.KnownIssues;
using Opus.Engine.AlphaStress.Memory;
using Opus.Engine.AlphaStress.Stress;
using Opus.Engine.AlphaStress.Tests.Support;
using Xunit;

namespace Opus.Engine.AlphaStress.Tests.Stress;

public sealed class AlphaStressHarnessTests
{
    [Fact]
    public void Run_null_profile_throws()
    {
        var runner = new FakeStressIterationRunner(Array.Empty<AlphaStressIterationRunResult>());

        var act = () => AlphaStressHarness.Run(null!, runner);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Run_null_runner_throws()
    {
        var act = () => AlphaStressHarness.Run(AlphaStressProfile.Default, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Run_clean_iterations_produce_clean_outcome()
    {
        var profile = BuildProfile(iterationCount: 3);
        var observations = BuildSteadyObservations(profile, frameMs: 10);
        var runner = new FakeStressIterationRunner(new[]
        {
            BuildCleanResult(profile, observations),
            BuildCleanResult(profile, observations),
            BuildCleanResult(profile, observations),
        });
        var probe = BuildStableProbe(sampleCount: profile.IterationCount + 1);

        var outcome = AlphaStressHarness.Run(profile, runner, probe);

        outcome.IsClean.Should().BeTrue();
        outcome.AllIterationsClean.Should().BeTrue();
        outcome.Iterations.Should().HaveCount(3);
        runner.Calls.Should().HaveCount(3);
        runner.Calls[0].Index.Should().Be(0);
        runner.Calls[2].Index.Should().Be(2);
    }

    [Fact]
    public void Run_iteration_throwing_translates_to_unhandled_exception_issue()
    {
        var profile = BuildProfile(iterationCount: 1);
        var runner = new ThrowingRunner();
        var probe = BuildStableProbe(sampleCount: 2);

        var outcome = AlphaStressHarness.Run(profile, runner, probe);

        outcome.IsClean.Should().BeFalse();
        outcome.Iterations.Should().ContainSingle();
        outcome.Iterations[0].SmokeOutcome.Should().BeNull();
        outcome.IssuesWithCode(AlphaStressIssueCode.IterationUnhandledException).Should().ContainSingle();
    }

    [Fact]
    public void Run_iteration_without_smoke_records_host_unavailable()
    {
        var profile = BuildProfile(iterationCount: 1);
        var runner = new FakeStressIterationRunner(new[]
        {
            new AlphaStressIterationRunResult(
                SmokeOutcome: null,
                FramePacingObservations: Array.Empty<FramePacingObservation>(),
                UnhandledException: null),
        });
        var probe = BuildStableProbe(sampleCount: 2);

        var outcome = AlphaStressHarness.Run(profile, runner, probe);

        outcome.IssuesWithCode(AlphaStressIssueCode.HostUnavailable).Should().ContainSingle();
    }

    [Fact]
    public void Run_iteration_with_unclean_smoke_records_iteration_failed()
    {
        var profile = BuildProfile(iterationCount: 1);
        var observations = BuildSteadyObservations(profile, frameMs: 10);
        var dirty = BuildDirtyResult(profile, observations);
        var runner = new FakeStressIterationRunner(new[] { dirty });
        var probe = BuildStableProbe(sampleCount: 2);

        var outcome = AlphaStressHarness.Run(profile, runner, probe);

        outcome.IssuesWithCode(AlphaStressIssueCode.IterationFailed).Should().ContainSingle();
    }

    [Fact]
    public void Run_records_frame_pacing_degraded_when_threshold_breached()
    {
        var profile = BuildProfile(iterationCount: 1) with
        {
            FramePacing = FramePacingThresholds.Default with
            {
                P95Limit = TimeSpan.FromMilliseconds(20),
                HitchThreshold = TimeSpan.FromMilliseconds(50),
                HitchCountLimit = 0,
            },
        };
        var hitchObservations = new[]
        {
            new FramePacingObservation(1, DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(10)),
            new FramePacingObservation(2, DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(60)),
        };
        var runner = new FakeStressIterationRunner(new[] { BuildCleanResult(profile, hitchObservations) });
        var probe = BuildStableProbe(sampleCount: 2);

        var outcome = AlphaStressHarness.Run(profile, runner, probe);

        outcome.IssuesWithCode(AlphaStressIssueCode.FramePacingDegraded).Should().ContainSingle();
    }

    [Fact]
    public void Run_records_memory_growth_when_threshold_breached()
    {
        var profile = BuildProfile(iterationCount: 1) with
        {
            Memory = MemoryProbeThresholds.Default with
            {
                ManagedHeapGrowthLimitBytes = 100,
                WorkingSetGrowthLimitBytes = 100,
                Gen2CollectionLimit = 0,
            },
        };
        var observations = BuildSteadyObservations(profile, frameMs: 10);
        var runner = new FakeStressIterationRunner(new[] { BuildCleanResult(profile, observations) });
        var baseTime = new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);
        var probe = new FakeMemoryProbe(new[]
        {
            new MemoryProbeSample(baseTime, ManagedHeapBytes: 1_000, WorkingSetBytes: 4_000, Gen0Collections: 0, Gen1Collections: 0, Gen2Collections: 0),
            new MemoryProbeSample(baseTime.AddSeconds(1), ManagedHeapBytes: 9_000, WorkingSetBytes: 12_000, Gen0Collections: 0, Gen1Collections: 0, Gen2Collections: 2),
        });

        var outcome = AlphaStressHarness.Run(profile, runner, probe);

        outcome.IssuesWithCode(AlphaStressIssueCode.MemoryGrowthExceeded).Should().ContainSingle();
    }

    [Fact]
    public void Run_records_blocker_when_ledger_has_open_blocker()
    {
        var profile = BuildProfile(iterationCount: 1);
        var observations = BuildSteadyObservations(profile, frameMs: 10);
        var runner = new FakeStressIterationRunner(new[] { BuildCleanResult(profile, observations) });
        var probe = BuildStableProbe(sampleCount: 2);
        var ledger = KnownIssueLedger.Create(new[]
        {
            new KnownIssueRecord("blk-1", KnownIssueSeverity.Blocker, KnownIssueStatus.Open, "summary", null, DateTimeOffset.UtcNow),
        });

        var outcome = AlphaStressHarness.Run(profile, runner, probe, knownIssues: ledger);

        outcome.IssuesWithCode(AlphaStressIssueCode.KnownIssueBlockerOpen).Should().ContainSingle();
    }

    [Fact]
    public void Run_records_must_fix_when_ledger_has_open_must_fix()
    {
        var profile = BuildProfile(iterationCount: 1);
        var observations = BuildSteadyObservations(profile, frameMs: 10);
        var runner = new FakeStressIterationRunner(new[] { BuildCleanResult(profile, observations) });
        var probe = BuildStableProbe(sampleCount: 2);
        var ledger = KnownIssueLedger.Create(new[]
        {
            new KnownIssueRecord("mst-1", KnownIssueSeverity.MustFix, KnownIssueStatus.Open, "summary", null, DateTimeOffset.UtcNow),
        });

        var outcome = AlphaStressHarness.Run(profile, runner, probe, knownIssues: ledger);

        outcome.IssuesWithCode(AlphaStressIssueCode.KnownIssueMustFixOpen).Should().ContainSingle();
    }

    [Fact]
    public void Run_aggregates_frame_pacing_across_iterations()
    {
        var profile = BuildProfile(iterationCount: 2);
        var iterationA = new[]
        {
            new FramePacingObservation(1, DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(10)),
            new FramePacingObservation(2, DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(20)),
        };
        var iterationB = new[]
        {
            new FramePacingObservation(3, DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(30)),
            new FramePacingObservation(4, DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(40)),
        };
        var runner = new FakeStressIterationRunner(new[]
        {
            BuildCleanResult(profile, iterationA),
            BuildCleanResult(profile, iterationB),
        });
        var probe = BuildStableProbe(sampleCount: 3);

        var outcome = AlphaStressHarness.Run(profile, runner, probe);

        outcome.AggregatedFramePacing.SampleCount.Should().Be(4);
        outcome.AggregatedFramePacing.Max.Should().Be(TimeSpan.FromMilliseconds(40));
    }

    [Fact]
    public void Run_records_budget_exceeded_when_wall_clock_runs_out()
    {
        var profile = BuildProfile(iterationCount: 5) with
        {
            WallClockBudget = TimeSpan.FromMilliseconds(1),
        };
        var observations = BuildSteadyObservations(profile, frameMs: 10);
        var runner = new SlowRunner(observations);
        var probe = BuildStableProbe(sampleCount: 6);

        var outcome = AlphaStressHarness.Run(profile, runner, probe);

        outcome.IssuesWithCode(AlphaStressIssueCode.BudgetExceeded).Should().NotBeEmpty();
        outcome.Iterations.Count.Should().BeLessThan(profile.IterationCount);
    }

    private static AlphaStressProfile BuildProfile(int iterationCount) => AlphaStressProfile.Default with
    {
        IterationCount = iterationCount,
        WallClockBudget = TimeSpan.FromSeconds(30),
        IterationProfile = AlphaSmokeProfile.Default with { FrameTarget = 2, SmokeName = "fake-stress" },
    };

    private static FramePacingObservation[] BuildSteadyObservations(AlphaStressProfile profile, int frameMs)
    {
        var observations = new FramePacingObservation[profile.IterationProfile.FrameTarget];
        for (var i = 0; i < observations.Length; i++)
        {
            observations[i] = new FramePacingObservation(
                FrameNumber: i + 1,
                ObservedAtUtc: DateTimeOffset.UtcNow,
                CpuFrameTime: TimeSpan.FromMilliseconds(frameMs));
        }

        return observations;
    }

    private static AlphaStressIterationRunResult BuildCleanResult(
        AlphaStressProfile profile,
        IReadOnlyList<FramePacingObservation> observations)
    {
        var smoke = AlphaSmokeOutcome.Create(
            profile.IterationProfile,
            DateTimeOffset.UtcNow,
            TimeSpan.FromMilliseconds(20),
            framesStepped: profile.IterationProfile.FrameTarget,
            meanCpuFrameTime: TimeSpan.FromMilliseconds(10),
            maxCpuFrameTime: TimeSpan.FromMilliseconds(15),
            p95CpuFrameTime: TimeSpan.FromMilliseconds(12),
            screenshotPath: null,
            issues: Array.Empty<AlphaSmokeIssue>());
        return new AlphaStressIterationRunResult(
            SmokeOutcome: smoke,
            FramePacingObservations: observations,
            UnhandledException: null);
    }

    private static AlphaStressIterationRunResult BuildDirtyResult(
        AlphaStressProfile profile,
        IReadOnlyList<FramePacingObservation> observations)
    {
        var smoke = AlphaSmokeOutcome.Create(
            profile.IterationProfile,
            DateTimeOffset.UtcNow,
            TimeSpan.FromMilliseconds(20),
            framesStepped: profile.IterationProfile.FrameTarget,
            meanCpuFrameTime: TimeSpan.FromMilliseconds(10),
            maxCpuFrameTime: TimeSpan.FromMilliseconds(15),
            p95CpuFrameTime: TimeSpan.FromMilliseconds(12),
            screenshotPath: null,
            issues: new[]
            {
                AlphaSmokeIssue.Create(AlphaSmokeIssueCode.HostStoppedEarly, "early stop", DateTimeOffset.UtcNow),
            });
        return new AlphaStressIterationRunResult(
            SmokeOutcome: smoke,
            FramePacingObservations: observations,
            UnhandledException: null);
    }

    private static FakeMemoryProbe BuildStableProbe(int sampleCount)
    {
        var baseTime = new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);
        var samples = new MemoryProbeSample[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            samples[i] = new MemoryProbeSample(
                baseTime.AddMilliseconds(i),
                ManagedHeapBytes: 1024,
                WorkingSetBytes: 4096,
                Gen0Collections: 0,
                Gen1Collections: 0,
                Gen2Collections: 0);
        }

        return new FakeMemoryProbe(samples);
    }

    private sealed class ThrowingRunner : IAlphaStressIterationRunner
    {
        public AlphaStressIterationRunResult Run(int iterationIndex, AlphaSmokeProfile iterationProfile) =>
            throw new InvalidOperationException("iteration runner exploded");
    }

    private sealed class SlowRunner : IAlphaStressIterationRunner
    {
        private readonly IReadOnlyList<FramePacingObservation> _observations;

        public SlowRunner(IReadOnlyList<FramePacingObservation> observations)
        {
            _observations = observations;
        }

        public AlphaStressIterationRunResult Run(int iterationIndex, AlphaSmokeProfile iterationProfile)
        {
            System.Threading.Thread.Sleep(10);
            var smoke = AlphaSmokeOutcome.Create(
                iterationProfile,
                DateTimeOffset.UtcNow,
                TimeSpan.FromMilliseconds(10),
                framesStepped: iterationProfile.FrameTarget,
                meanCpuFrameTime: TimeSpan.FromMilliseconds(5),
                maxCpuFrameTime: TimeSpan.FromMilliseconds(10),
                p95CpuFrameTime: TimeSpan.FromMilliseconds(7),
                screenshotPath: null,
                issues: Array.Empty<AlphaSmokeIssue>());
            return new AlphaStressIterationRunResult(smoke, _observations, UnhandledException: null);
        }
    }
}
