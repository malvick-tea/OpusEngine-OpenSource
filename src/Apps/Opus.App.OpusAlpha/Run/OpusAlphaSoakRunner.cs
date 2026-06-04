using System;
using System.Globalization;
using Opus.App.OpusAlpha.Cli;
using Opus.Engine.AlphaHarness.Soak;
using Opus.Engine.Net.Soak;
using Opus.Foundation;

namespace Opus.App.OpusAlpha.Run;

/// <summary>
/// Runs the loopback soak (Mode = Soak). Wires <see cref="AlphaLoopbackSoakRig"/> against
/// <see cref="NetSoakHarness"/> using the soak knobs from <see cref="OpusAlphaArgs"/> and
/// prints the structured report. Exit code reflects whether the run was clean.
/// </summary>
public static class OpusAlphaSoakRunner
{
    private const int ExitClean = 0;
    private const int ExitDirty = 2;

    /// <summary>Runs the soak; returns the conventional exit code.</summary>
    public static int Run(OpusAlphaArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        var profile = new NetSoakProfile(
            PeerCount: args.SoakPeers,
            PacketsPerPeer: args.SoakPacketsPerPeer,
            PayloadBytes: args.SoakPayloadBytes,
            EchoFromServer: true,
            ConnectBudget: TimeSpan.FromSeconds(3),
            WorkloadBudget: TimeSpan.FromSeconds(10));
        try
        {
            profile.Validate();
        }
        catch (ArgumentException ex)
        {
            log.Error("Soak profile is invalid; aborting.", ex);
            return ExitDirty;
        }

        using var rig = AlphaLoopbackSoakRig.Create(profile.PeerCount);
        var report = NetSoakHarness.Run(profile, rig);
        PrintReport(log, report);
        return report.IsClean ? ExitClean : ExitDirty;
    }

    private static void PrintReport(ILog log, NetSoakReport report)
    {
        log.Info(string.Create(
            CultureInfo.InvariantCulture,
            $"Loopback soak: peers={report.Profile.PeerCount} packets/peer={report.Profile.PacketsPerPeer} elapsedMs={report.ElapsedWallClock.TotalMilliseconds:F0} clean={report.IsClean}."));
        log.Info(string.Create(
            CultureInfo.InvariantCulture,
            $"  sent={report.TotalBytesSent} serverReceivedBytes={report.TotalBytesServerReceived} serverReceivedPackets={report.TotalPacketsServerReceived} echoReceivedPackets={report.TotalEchoPacketsReceived}."));
        foreach (var issue in report.Issues)
        {
            log.Warn($"{issue.DiagnosticCode} peer={issue.PeerIndex.ToString(CultureInfo.InvariantCulture)} {issue.Detail}");
        }
    }
}
