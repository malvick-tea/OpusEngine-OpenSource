using System;
using System.Globalization;
using Opus.Engine.Net.Session;

namespace Opus.Engine.Net.Telemetry;

/// <summary>
/// Pure formatting helpers that turn <see cref="NetSessionTelemetry"/> and
/// <see cref="NetSessionEvent"/> into stable, culture-invariant strings for log entries,
/// failure-report lines, and overlay rows. Stateless and side-effect-free; the same
/// inputs always produce the same outputs across runs and platforms.
/// </summary>
public static class NetSessionTelemetryFormatter
{
    /// <summary>Compact one-line status string ("connected, 5 peers, 12345 in / 9876 out").</summary>
    public static string FormatStatusLine(NetSessionTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);
        var stats = telemetry.Statistics;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{FormatState(telemetry.State)}, {stats.ConnectedPeerCount} peers, {stats.PacketsReceived} in / {stats.PacketsSent} out, {stats.PeersAcceptedTotal} joined, {stats.PeersDisconnectedTotal} left, {stats.ReconnectAttempts} reconnects, {stats.QueuedPayloadsDropped} dropped");
    }

    /// <summary>One-line RTT summary string ("rtt n=12 mean=18.4ms p95=45.0ms").
    /// Returns "rtt n=0" when no consumer ping protocol has fed observations yet.</summary>
    public static string FormatRttLine(NetSessionTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);
        var rtt = telemetry.Statistics.Rtt;
        if (rtt.SampleCount == 0)
        {
            return "rtt n=0";
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"rtt n={rtt.SampleCount} mean={rtt.Mean.TotalMilliseconds:F1}ms min={rtt.Minimum.TotalMilliseconds:F1}ms max={rtt.Maximum.TotalMilliseconds:F1}ms p95={rtt.Percentile95.TotalMilliseconds:F1}ms");
    }

    /// <summary>One-line per-second rate string
    /// ("rate 60.0pps in / 30.0pps out, 6000B/s in / 3000B/s out, window=2.00s").
    /// Returns "rate window=0.00s" while no rate window has been computed yet.</summary>
    public static string FormatRateLine(NetSessionTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);
        var rate = telemetry.Statistics.Rate;
        if (rate.WindowDuration <= TimeSpan.Zero)
        {
            return "rate window=0.00s";
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"rate {rate.PacketsReceivedPerSecond:F1}pps in / {rate.PacketsSentPerSecond:F1}pps out, {rate.BytesReceivedPerSecond:F0}B/s in / {rate.BytesSentPerSecond:F0}B/s out, window={rate.WindowDuration.TotalSeconds:F2}s");
    }

    /// <summary>One-line untrusted-input guard summary
    /// ("guards rejectedConn=0 droppedInbound=0 rateLimited=0"). Surfaces the connection-flood,
    /// inbound-queue-cap, and per-peer rate-limit shedding of a server session's transport; all zero
    /// for a client session or a transport without the diagnostics capability.</summary>
    public static string FormatGuardLine(NetSessionTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);
        var guards = telemetry.Statistics.TransportGuards;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"guards rejectedConn={guards.RejectedConnections} droppedInbound={guards.DroppedInboundPayloads} rateLimited={guards.RateLimitedInboundPayloads}");
    }

    /// <summary>Short detail string suitable for an overlay row.</summary>
    public static string FormatDetailLine(NetSessionTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);
        var fault = telemetry.LastFault;
        if (fault is not null && telemetry.State == NetSessionState.Faulted)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{telemetry.DisplayName} | {fault.Code}: {fault.Detail}");
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{telemetry.DisplayName} | {FormatRole(telemetry.Role)}");
    }

    /// <summary>Stable log line for a session event. The diagnostic code is included
    /// when present so log filters can locate the exact entry.</summary>
    public static string FormatEventLine(NetSessionEvent sessionEvent)
    {
        ArgumentNullException.ThrowIfNull(sessionEvent);
        var code = string.IsNullOrEmpty(sessionEvent.DiagnosticCode) ? "OPDX-NET-???" : sessionEvent.DiagnosticCode;
        var peer = sessionEvent.Peer.IsValid
            ? sessionEvent.Peer.ToString()
            : "-";
        var detail = sessionEvent.Detail ?? string.Empty;
        var formatted = string.Create(
            CultureInfo.InvariantCulture,
            $"{code} [{sessionEvent.Kind}] peer={peer} peers={sessionEvent.ConnectedPeerCount} bytes={sessionEvent.PayloadByteCount} {detail}");
        return formatted.TrimEnd();
    }

    private static string FormatRole(NetSessionRole role) => role switch
    {
        NetSessionRole.Client => "client",
        NetSessionRole.Server => "server",
        _ => role.ToString(),
    };

    private static string FormatState(NetSessionState state) => state switch
    {
        NetSessionState.Idle => "idle",
        NetSessionState.Connecting => "connecting",
        NetSessionState.Connected => "connected",
        NetSessionState.Reconnecting => "reconnecting",
        NetSessionState.Faulted => "faulted",
        NetSessionState.Disposed => "disposed",
        _ => state.ToString(),
    };
}
