using System;
using Opus.Engine.Net.Session;

namespace Opus.Engine.Net.Telemetry;

/// <summary>
/// Renderer-neutral telemetry snapshot for a single <see cref="INetSession"/>. The host
/// composes one per overlay refresh and a failure-report writer composes one when a
/// report is captured. Carries the smallest stable shape that the engine layer can serve
/// without depending on transport-specific concepts.
/// </summary>
/// <param name="DisplayName">Display name of the session.</param>
/// <param name="Role">Role this session was constructed for.</param>
/// <param name="State">Current state machine position.</param>
/// <param name="Statistics">Latest rolling statistics snapshot.</param>
/// <param name="LastFault">Most recently recorded session fault, or <c>null</c>.</param>
public sealed record NetSessionTelemetry(
    string DisplayName,
    NetSessionRole Role,
    NetSessionState State,
    NetSessionStatisticsSnapshot Statistics,
    NetSessionFault? LastFault)
{
    /// <summary>Captures a fresh telemetry snapshot from a running session.</summary>
    public static NetSessionTelemetry Capture(INetSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new NetSessionTelemetry(
            session.DisplayName,
            session.Role,
            session.State,
            session.Statistics,
            session.LastFault);
    }

    /// <summary>Synthetic snapshot for hosts that have not wired a network session yet.</summary>
    public static NetSessionTelemetry Unconfigured(string displayName, DateTimeOffset capturedAtUtc) => new(
        DisplayName: displayName,
        Role: NetSessionRole.Client,
        State: NetSessionState.Idle,
        Statistics: NetSessionStatisticsSnapshot.Empty(capturedAtUtc),
        LastFault: null);
}
