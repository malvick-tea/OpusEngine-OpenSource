using System;
using Opus.App.OpusAlpha.Cli;
using Opus.Engine.AlphaHarness.Smoke;
using Opus.Engine.AlphaStress.KnownIssues;
using Opus.Engine.AlphaStress.Memory;
using Opus.Engine.AlphaStress.Network;
using Opus.Engine.AlphaStress.Stress;
using Opus.Engine.Diagnostics.Reports;
using Opus.Engine.Host.Windows.Direct3D12;
using Opus.Engine.Net.Soak;
using Opus.Engine.Net.Transport;
using Opus.Foundation;

namespace Opus.App.OpusAlpha.Run;

/// <summary>
/// Stress runner — drives the M11 alpha stress harness for the requested iteration
/// count over fresh D3D12 host instances, evaluates frame-pacing and memory thresholds,
/// optionally loads a known-issue ledger, and writes a paired JSON+TXT report. The exit
/// code is 0 on a clean run, 1 when the harness could not produce a single iteration,
/// and 2 when issues were recorded.
/// </summary>
public static class OpusAlphaStressRunner
{
    private const int ExitClean = 0;
    private const int ExitHostUnavailable = 1;
    private const int ExitStressIssues = 2;

    /// <summary>Runs the stress run described by <paramref name="args"/>.</summary>
    public static int Run(OpusAlphaArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        var profile = BuildProfile(args);
        try
        {
            profile.Validate();
        }
        catch (ArgumentException ex)
        {
            log.Error("Stress profile is invalid; aborting.", ex);
            return ExitHostUnavailable;
        }

        var hostOptions = BuildHostOptions(args);
        var iterationRunner = new OpusAlphaStressIterationRunner(hostOptions, log);
        var ledger = OpusAlphaKnownIssuesLoader.Load(args.KnownIssuesPath, log);
        var probe = new SystemMemoryProbe();
        var outcome = AlphaStressHarness.Run(profile, iterationRunner, probe, knownIssues: ledger);
        var writer = CreateReportWriter(args, hostOptions);
        WriteOutcome(writer, outcome, log);
        return ResolveExitCode(outcome);
    }

    private static AlphaStressProfile BuildProfile(OpusAlphaArgs args)
    {
        var iterationProfile = AlphaSmokeProfile.Default with
        {
            FrameTarget = args.SmokeFrameCount,
            SmokeName = "opus-alpha-stress-iter",
        };
        var profile = AlphaStressProfile.Default with
        {
            IterationProfile = iterationProfile,
            IterationCount = args.StressIterations,
        };

        if (args.EnableStressNetworkInjection)
        {
            profile = profile with { Network = BuildNetworkProfile(args) };
        }

        return profile;
    }

    private static AlphaStressNetworkProfile BuildNetworkProfile(OpusAlphaArgs args) => new(
        Injection: new LatencyLossInjectionProfile(
            LossRate: args.StressInjectionLossRate,
            AddedLatency: TimeSpan.FromMilliseconds(args.StressInjectionLatencyMilliseconds),
            Seed: args.StressInjectionSeed)
        {
            InboundLossRate = args.StressInjectionInboundLossRate,
            InboundAddedLatency = TimeSpan.FromMilliseconds(args.StressInjectionInboundLatencyMilliseconds),
            InboundSeed = args.StressInjectionInboundSeed,
        },
        Soak: new NetSoakProfile(
            PeerCount: args.StressInjectionPeers,
            PacketsPerPeer: args.StressInjectionPacketsPerPeer,
            PayloadBytes: args.StressInjectionPayloadBytes,
            EchoFromServer: true,
            ConnectBudget: TimeSpan.FromSeconds(3),
            WorkloadBudget: TimeSpan.FromSeconds(5)),
        Tolerance: new AlphaStressFaultInjectionTolerance(
            MaxDropRate: args.StressInjectionDropTolerance,
            MaxObservedSoakIssues: 0)
        {
            MaxInboundDropRate = args.StressInjectionInboundDropTolerance,
        });

    private static D3D12OpusApplicationOptions BuildHostOptions(OpusAlphaArgs args)
    {
        var options = D3D12OpusApplicationOptions.Default with
        {
            SceneScale = args.SceneScale,
        };
        if (!string.IsNullOrWhiteSpace(args.AssetPath))
        {
            options = options with { AssetPath = args.AssetPath };
        }

        if (!string.IsNullOrWhiteSpace(args.DiagnosticsDirectory))
        {
            options = options with { DiagnosticsDirectory = args.DiagnosticsDirectory };
        }

        return options;
    }

    private static AlphaStressReportWriter CreateReportWriter(
        OpusAlphaArgs args,
        D3D12OpusApplicationOptions hostOptions)
    {
        var directory = string.IsNullOrWhiteSpace(args.StressReportDirectory)
            ? OpusDiagnosticsPaths.StressDirectory(hostOptions.EffectiveDiagnosticsDirectory)
            : args.StressReportDirectory;
        return new AlphaStressReportWriter(new AlphaStressReportWriterOptions(directory, OpusAlphaRetention.Artifacts));
    }

    private static void WriteOutcome(AlphaStressReportWriter writer, AlphaStressOutcome outcome, ILog log)
    {
        var result = writer.Write(outcome);
        if (result.IsSuccess)
        {
            log.Info($"Stress report written: {result.TextPath}");
            return;
        }

        var issue = result.Issue;
        log.Error(issue is null
            ? "Stress report write failed without a structured issue."
            : $"{issue.Code}: {issue.Message} Remediation: {issue.RemediationHint}");
    }

    private static int ResolveExitCode(AlphaStressOutcome outcome)
    {
        if (outcome.IsClean)
        {
            return ExitClean;
        }

        foreach (var issue in outcome.Issues)
        {
            if (issue.Code == AlphaStressIssueCode.HostUnavailable)
            {
                return ExitHostUnavailable;
            }
        }

        return ExitStressIssues;
    }
}
