using System;
using System.Collections.Generic;
using Opus.Engine.Net.Session;

namespace Opus.Engine.Net.Soak;

/// <summary>
/// Outcome of a complete <see cref="NetSoakHarness"/> run. Captures the wall-clock
/// duration, per-peer outcomes, every observed issue, and the totals the host needs to
/// emit a single-line summary into log or telemetry overlays.
/// </summary>
/// <param name="ServerGuards">The server transport's untrusted-input guard counts at the end of the
/// run (connection-flood rejects, inbound-queue-cap drops, per-peer rate-limit sheds), read through
/// <see cref="INetServerTransportDiagnostics"/>. <see cref="NetTransportGuardCounts.None"/> when the
/// rig's server transport does not expose the capability (the in-process loopback hub). A non-zero
/// value distinguishes a guard shedding load from genuine packet loss when a stress profile pushes
/// past the caps.</param>
public sealed record NetSoakReport(
    NetSoakProfile Profile,
    TimeSpan ElapsedWallClock,
    IReadOnlyList<NetSoakPeerReport> Peers,
    IReadOnlyList<NetSoakIssue> Issues,
    long TotalPacketsExpected,
    long TotalPacketsServerReceived,
    long TotalEchoPacketsReceived,
    long TotalBytesSent,
    long TotalBytesServerReceived,
    NetTransportGuardCounts ServerGuards)
{
    /// <summary>True when no issues were recorded; alpha-quality runs treat this as the
    /// pass/fail gate for a smoke check.</summary>
    public bool IsClean => Issues.Count == 0;

    /// <summary>Returns issues with the supplied code; never null.</summary>
    public IEnumerable<NetSoakIssue> IssuesWithCode(NetSoakIssueCode code)
    {
        foreach (var issue in Issues)
        {
            if (issue.Code == code)
            {
                yield return issue;
            }
        }
    }
}
