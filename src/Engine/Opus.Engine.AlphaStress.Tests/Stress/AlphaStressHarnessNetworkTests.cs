using System;
using System.Collections.Generic;
using FluentAssertions;
using Opus.Engine.AlphaHarness.Smoke;
using Opus.Engine.AlphaStress.FramePacing;
using Opus.Engine.AlphaStress.Network;
using Opus.Engine.AlphaStress.Stress;
using Opus.Engine.AlphaStress.Tests.Support;
using Opus.Engine.Net.Soak;
using Opus.Engine.Net.Transport;
using Xunit;

namespace Opus.Engine.AlphaStress.Tests.Stress;

public sealed class AlphaStressHarnessNetworkTests
{
    [Fact]
    public void Run_without_network_profile_skips_probe()
    {
        var profile = BuildBaseProfile(iterationCount: 2);
        var runner = new FakeStressIterationRunner(new[]
        {
            BuildCleanResult(profile),
            BuildCleanResult(profile),
        });
        var probe = new FakeNetworkProbe(Array.Empty<AlphaStressNetworkObservation>());
        var memoryProbe = BuildStableProbe(profile.IterationCount + 1);

        AlphaStressHarness.Run(profile, runner, memoryProbe, probe);

        probe.Calls.Should().BeEmpty();
    }

    [Fact]
    public void Run_with_network_profile_drives_probe_per_iteration()
    {
        var profile = BuildNetworkProfile(iterationCount: 3);
        var runner = new FakeStressIterationRunner(new[]
        {
            BuildCleanResult(profile),
            BuildCleanResult(profile),
            BuildCleanResult(profile),
        });
        var probe = new FakeNetworkProbe(new[]
        {
            BuildObservation(0, sends: 100, drops: 10, delays: 50, soakIssues: 0),
            BuildObservation(1, sends: 100, drops: 10, delays: 50, soakIssues: 0),
            BuildObservation(2, sends: 100, drops: 10, delays: 50, soakIssues: 0),
        });
        var memoryProbe = BuildStableProbe(profile.IterationCount + 1);

        var outcome = AlphaStressHarness.Run(profile, runner, memoryProbe, probe);

        probe.Calls.Should().Equal(0, 1, 2);
        outcome.Network.IterationCount.Should().Be(3);
        outcome.Network.TotalClientSendAttempts.Should().Be(300);
        outcome.Network.TotalDroppedPackets.Should().Be(30);
        outcome.IsClean.Should().BeTrue();
    }

    [Fact]
    public void Run_emits_fault_injection_degraded_when_drop_fraction_exceeds_tolerance()
    {
        var profile = BuildNetworkProfile(iterationCount: 1) with
        {
            Network = AlphaStressNetworkProfile.Default with
            {
                Tolerance = new AlphaStressFaultInjectionTolerance(MaxDropRate: 0.10, MaxObservedSoakIssues: 0),
            },
        };
        var runner = new FakeStressIterationRunner(new[] { BuildCleanResult(profile) });
        var probe = new FakeNetworkProbe(new[]
        {
            BuildObservation(0, sends: 100, drops: 50, delays: 30, soakIssues: 0),
        });
        var memoryProbe = BuildStableProbe(2);

        var outcome = AlphaStressHarness.Run(profile, runner, memoryProbe, probe);

        outcome.IssuesWithCode(AlphaStressIssueCode.FaultInjectionDegraded).Should().ContainSingle();
    }

    [Fact]
    public void Run_emits_fault_injection_degraded_when_soak_issue_count_exceeds_tolerance()
    {
        var profile = BuildNetworkProfile(iterationCount: 1);
        var runner = new FakeStressIterationRunner(new[] { BuildCleanResult(profile) });
        var probe = new FakeNetworkProbe(new[]
        {
            BuildObservation(0, sends: 100, drops: 0, delays: 0, soakIssues: 3),
        });
        var memoryProbe = BuildStableProbe(2);

        var outcome = AlphaStressHarness.Run(profile, runner, memoryProbe, probe);

        outcome.IssuesWithCode(AlphaStressIssueCode.FaultInjectionDegraded).Should().ContainSingle();
    }

    [Fact]
    public void Run_clean_network_observation_emits_no_fault_injection_issue()
    {
        var profile = BuildNetworkProfile(iterationCount: 1);
        var runner = new FakeStressIterationRunner(new[] { BuildCleanResult(profile) });
        var probe = new FakeNetworkProbe(new[]
        {
            BuildObservation(0, sends: 100, drops: 5, delays: 5, soakIssues: 0),
        });
        var memoryProbe = BuildStableProbe(2);

        var outcome = AlphaStressHarness.Run(profile, runner, memoryProbe, probe);

        outcome.IssuesWithCode(AlphaStressIssueCode.FaultInjectionDegraded).Should().BeEmpty();
    }

    [Fact]
    public void Run_does_not_dispose_caller_supplied_network_probe()
    {
        var profile = BuildNetworkProfile(iterationCount: 1);
        var runner = new FakeStressIterationRunner(new[] { BuildCleanResult(profile) });
        var probe = new FakeNetworkProbe(new[]
        {
            BuildObservation(0, sends: 0, drops: 0, delays: 0, soakIssues: 0),
        });
        var memoryProbe = BuildStableProbe(2);

        AlphaStressHarness.Run(profile, runner, memoryProbe, probe);

        probe.Disposed.Should().BeFalse();
    }

    [Fact]
    public void Run_emits_fault_injection_degraded_when_inbound_drop_fraction_exceeds_tolerance()
    {
        var profile = BuildNetworkProfile(iterationCount: 1) with
        {
            Network = AlphaStressNetworkProfile.Default with
            {
                Tolerance = new AlphaStressFaultInjectionTolerance(
                    MaxDropRate: 1.0,
                    MaxObservedSoakIssues: 0)
                {
                    MaxInboundDropRate = 0.10,
                },
            },
        };
        var runner = new FakeStressIterationRunner(new[] { BuildCleanResult(profile) });
        var probe = new FakeNetworkProbe(new[]
        {
            BuildObservation(0, sends: 100, drops: 0, delays: 0, soakIssues: 0) with
            {
                InboundAttempts = 100,
                InboundDroppedPackets = 50,
                InboundDelayedPackets = 20,
            },
        });
        var memoryProbe = BuildStableProbe(2);

        var outcome = AlphaStressHarness.Run(profile, runner, memoryProbe, probe);

        outcome.IssuesWithCode(AlphaStressIssueCode.FaultInjectionDegraded).Should().ContainSingle();
    }

    [Fact]
    public void Run_clean_inbound_observation_emits_no_fault_injection_issue()
    {
        var profile = BuildNetworkProfile(iterationCount: 1) with
        {
            Network = AlphaStressNetworkProfile.Default with
            {
                Tolerance = new AlphaStressFaultInjectionTolerance(
                    MaxDropRate: 1.0,
                    MaxObservedSoakIssues: 0)
                {
                    MaxInboundDropRate = 0.25,
                },
            },
        };
        var runner = new FakeStressIterationRunner(new[] { BuildCleanResult(profile) });
        var probe = new FakeNetworkProbe(new[]
        {
            BuildObservation(0, sends: 100, drops: 0, delays: 0, soakIssues: 0) with
            {
                InboundAttempts = 100,
                InboundDroppedPackets = 10,
                InboundDelayedPackets = 5,
            },
        });
        var memoryProbe = BuildStableProbe(2);

        var outcome = AlphaStressHarness.Run(profile, runner, memoryProbe, probe);

        outcome.IssuesWithCode(AlphaStressIssueCode.FaultInjectionDegraded).Should().BeEmpty();
    }

    private static AlphaStressProfile BuildBaseProfile(int iterationCount) => AlphaStressProfile.Default with
    {
        IterationCount = iterationCount,
        WallClockBudget = TimeSpan.FromSeconds(30),
        IterationProfile = AlphaSmokeProfile.Default with { FrameTarget = 2, SmokeName = "fake-stress-net" },
    };

    private static AlphaStressProfile BuildNetworkProfile(int iterationCount) =>
        BuildBaseProfile(iterationCount) with
        {
            Network = AlphaStressNetworkProfile.Default with
            {
                Injection = LatencyLossInjectionProfile.None,
                Soak = new NetSoakProfile(
                    PeerCount: 2,
                    PacketsPerPeer: 4,
                    PayloadBytes: 16,
                    EchoFromServer: false,
                    ConnectBudget: TimeSpan.FromMilliseconds(200),
                    WorkloadBudget: TimeSpan.FromMilliseconds(200)),
                Tolerance = AlphaStressFaultInjectionTolerance.Default,
            },
        };

    private static AlphaStressIterationRunResult BuildCleanResult(AlphaStressProfile profile)
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
        var observations = new[]
        {
            new FramePacingObservation(1, DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(10)),
            new FramePacingObservation(2, DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(10)),
        };
        return new AlphaStressIterationRunResult(
            SmokeOutcome: smoke,
            FramePacingObservations: observations,
            UnhandledException: null);
    }

    private static AlphaStressNetworkObservation BuildObservation(
        int iterationIndex,
        long sends,
        long drops,
        long delays,
        int soakIssues) => new(
            IterationIndex: iterationIndex,
            ObservedAtUtc: DateTimeOffset.UtcNow,
            ClientSendAttempts: sends,
            DroppedPackets: drops,
            DelayedPackets: delays,
            SoakIssueCount: soakIssues);

    private static FakeMemoryProbe BuildStableProbe(int sampleCount)
    {
        var baseTime = new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);
        var samples = new global::Opus.Engine.AlphaStress.Memory.MemoryProbeSample[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            samples[i] = new global::Opus.Engine.AlphaStress.Memory.MemoryProbeSample(
                baseTime.AddMilliseconds(i),
                ManagedHeapBytes: 1024,
                WorkingSetBytes: 4096,
                Gen0Collections: 0,
                Gen1Collections: 0,
                Gen2Collections: 0);
        }

        return new FakeMemoryProbe(samples);
    }
}
