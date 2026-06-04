using System;
using System.Collections.Generic;
using FluentAssertions;
using Opus.Engine.AlphaStress.FramePacing;
using Opus.Engine.AlphaStress.Memory;
using Opus.Engine.AlphaStress.Network;
using Opus.Engine.AlphaStress.Stress;
using Xunit;

namespace Opus.Engine.AlphaStress.Tests.Stress;

public sealed class AlphaStressOutcomeTests
{
    [Fact]
    public void Create_orders_iterations_by_index()
    {
        var profile = AlphaStressProfile.Default;
        var iterations = new[]
        {
            BuildIteration(2),
            BuildIteration(0),
            BuildIteration(1),
        };

        var outcome = AlphaStressOutcome.Create(
            profile,
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(3),
            iterations,
            FramePacingSummary.Empty(profile.FramePacing.HitchThreshold),
            MemoryProbeSummary.Empty,
            AlphaStressNetworkSummary.Empty,
            Array.Empty<AlphaStressIssue>());

        outcome.Iterations.Should().HaveCount(3);
        outcome.Iterations[0].IterationIndex.Should().Be(0);
        outcome.Iterations[1].IterationIndex.Should().Be(1);
        outcome.Iterations[2].IterationIndex.Should().Be(2);
    }

    [Fact]
    public void Create_sorts_issues_by_observed_time_then_code()
    {
        var profile = AlphaStressProfile.Default;
        var baseTime = DateTimeOffset.UtcNow;
        var issues = new[]
        {
            AlphaStressIssue.Global(AlphaStressIssueCode.MemoryGrowthExceeded, "memory", baseTime.AddSeconds(2)),
            AlphaStressIssue.Global(AlphaStressIssueCode.BudgetExceeded, "budget", baseTime),
            AlphaStressIssue.Global(AlphaStressIssueCode.FramePacingDegraded, "pacing", baseTime),
        };

        var outcome = AlphaStressOutcome.Create(
            profile,
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(3),
            Array.Empty<AlphaStressIterationOutcome>(),
            FramePacingSummary.Empty(profile.FramePacing.HitchThreshold),
            MemoryProbeSummary.Empty,
            AlphaStressNetworkSummary.Empty,
            issues);

        outcome.Issues.Should().HaveCount(3);
        outcome.Issues[0].Code.Should().Be(AlphaStressIssueCode.BudgetExceeded);
        outcome.Issues[1].Code.Should().Be(AlphaStressIssueCode.FramePacingDegraded);
        outcome.Issues[2].Code.Should().Be(AlphaStressIssueCode.MemoryGrowthExceeded);
    }

    [Fact]
    public void Create_rejects_negative_elapsed()
    {
        var profile = AlphaStressProfile.Default;

        var act = () => AlphaStressOutcome.Create(
            profile,
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(-1),
            Array.Empty<AlphaStressIterationOutcome>(),
            FramePacingSummary.Empty(profile.FramePacing.HitchThreshold),
            MemoryProbeSummary.Empty,
            AlphaStressNetworkSummary.Empty,
            Array.Empty<AlphaStressIssue>());

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_null_arguments_throw()
    {
        var profile = AlphaStressProfile.Default;
        var pacing = FramePacingSummary.Empty(profile.FramePacing.HitchThreshold);

        FluentActions.Invoking(() => AlphaStressOutcome.Create(
            null!, DateTimeOffset.UtcNow, TimeSpan.Zero, Array.Empty<AlphaStressIterationOutcome>(), pacing, MemoryProbeSummary.Empty, AlphaStressNetworkSummary.Empty, Array.Empty<AlphaStressIssue>()))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => AlphaStressOutcome.Create(
            profile, DateTimeOffset.UtcNow, TimeSpan.Zero, null!, pacing, MemoryProbeSummary.Empty, AlphaStressNetworkSummary.Empty, Array.Empty<AlphaStressIssue>()))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => AlphaStressOutcome.Create(
            profile, DateTimeOffset.UtcNow, TimeSpan.Zero, Array.Empty<AlphaStressIterationOutcome>(), null!, MemoryProbeSummary.Empty, AlphaStressNetworkSummary.Empty, Array.Empty<AlphaStressIssue>()))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => AlphaStressOutcome.Create(
            profile, DateTimeOffset.UtcNow, TimeSpan.Zero, Array.Empty<AlphaStressIterationOutcome>(), pacing, null!, AlphaStressNetworkSummary.Empty, Array.Empty<AlphaStressIssue>()))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => AlphaStressOutcome.Create(
            profile, DateTimeOffset.UtcNow, TimeSpan.Zero, Array.Empty<AlphaStressIterationOutcome>(), pacing, MemoryProbeSummary.Empty, null!, Array.Empty<AlphaStressIssue>()))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => AlphaStressOutcome.Create(
            profile, DateTimeOffset.UtcNow, TimeSpan.Zero, Array.Empty<AlphaStressIterationOutcome>(), pacing, MemoryProbeSummary.Empty, AlphaStressNetworkSummary.Empty, null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IsClean_true_when_no_issues_recorded()
    {
        var profile = AlphaStressProfile.Default;

        var outcome = AlphaStressOutcome.Create(
            profile,
            DateTimeOffset.UtcNow,
            TimeSpan.Zero,
            Array.Empty<AlphaStressIterationOutcome>(),
            FramePacingSummary.Empty(profile.FramePacing.HitchThreshold),
            MemoryProbeSummary.Empty,
            AlphaStressNetworkSummary.Empty,
            Array.Empty<AlphaStressIssue>());

        outcome.IsClean.Should().BeTrue();
    }

    [Fact]
    public void AllIterationsClean_true_when_every_iteration_clean()
    {
        var profile = AlphaStressProfile.Default;

        var outcome = AlphaStressOutcome.Create(
            profile,
            DateTimeOffset.UtcNow,
            TimeSpan.Zero,
            new[] { BuildIteration(0), BuildIteration(1) },
            FramePacingSummary.Empty(profile.FramePacing.HitchThreshold),
            MemoryProbeSummary.Empty,
            AlphaStressNetworkSummary.Empty,
            Array.Empty<AlphaStressIssue>());

        outcome.AllIterationsClean.Should().BeTrue();
    }

    [Fact]
    public void IssuesWithCode_filters_by_code()
    {
        var profile = AlphaStressProfile.Default;
        var baseTime = DateTimeOffset.UtcNow;
        var outcome = AlphaStressOutcome.Create(
            profile,
            DateTimeOffset.UtcNow,
            TimeSpan.Zero,
            Array.Empty<AlphaStressIterationOutcome>(),
            FramePacingSummary.Empty(profile.FramePacing.HitchThreshold),
            MemoryProbeSummary.Empty,
            AlphaStressNetworkSummary.Empty,
            new[]
            {
                AlphaStressIssue.Global(AlphaStressIssueCode.MemoryGrowthExceeded, "memory", baseTime),
                AlphaStressIssue.Global(AlphaStressIssueCode.BudgetExceeded, "budget", baseTime.AddSeconds(1)),
            });

        outcome.IssuesWithCode(AlphaStressIssueCode.MemoryGrowthExceeded).Should().ContainSingle();
        outcome.IssuesWithCode(AlphaStressIssueCode.BudgetExceeded).Should().ContainSingle();
        outcome.IssuesWithCode(AlphaStressIssueCode.FramePacingDegraded).Should().BeEmpty();
    }

    private static AlphaStressIterationOutcome BuildIteration(int index)
    {
        var profile = AlphaStressProfile.Default;
        var pacing = FramePacingSummary.Empty(profile.FramePacing.HitchThreshold);
        var smoke = global::Opus.Engine.AlphaHarness.Smoke.AlphaSmokeOutcome.Create(
            profile.IterationProfile,
            DateTimeOffset.UtcNow,
            TimeSpan.FromMilliseconds(50),
            framesStepped: profile.IterationProfile.FrameTarget,
            meanCpuFrameTime: TimeSpan.FromMilliseconds(10),
            maxCpuFrameTime: TimeSpan.FromMilliseconds(15),
            p95CpuFrameTime: TimeSpan.FromMilliseconds(12),
            screenshotPath: null,
            issues: Array.Empty<global::Opus.Engine.AlphaHarness.Smoke.AlphaSmokeIssue>());
        return new AlphaStressIterationOutcome(
            IterationIndex: index,
            StartedAtUtc: DateTimeOffset.UtcNow,
            ElapsedWallClock: TimeSpan.FromMilliseconds(50),
            SmokeOutcome: smoke,
            FramePacing: pacing,
            UnhandledExceptionMessage: null);
    }
}
