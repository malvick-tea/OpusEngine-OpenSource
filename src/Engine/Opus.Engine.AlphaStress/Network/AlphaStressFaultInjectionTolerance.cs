using System;

namespace Opus.Engine.AlphaStress.Network;

/// <summary>
/// Engine-neutral pass/fail thresholds applied against an aggregated
/// <see cref="AlphaStressNetworkSummary"/>. The stress harness emits
/// <c>StressFaultInjectionDegraded</c> (<c>OPDX-STR-006</c>) when the run's observed
/// drop fraction or inner soak issue count exceeds these tolerances. Pure data — the
/// harness reads the record once and never mutates it.
/// </summary>
/// <param name="MaxDropRate">Maximum fraction of attempted client sends the wrapping
/// transport is allowed to drop across the run. Computed as
/// <c>DroppedPackets / ClientSendAttempts</c>. Range <c>[0,1]</c>. Default of
/// <c>0.25</c> leaves enough headroom for a <c>0.10</c>-rate injection profile to pass
/// before flagging a degraded run.</param>
/// <param name="MaxObservedSoakIssues">Maximum number of inner <see cref="Opus.Engine.Net.Soak.NetSoakIssue"/>
/// observations tolerated across the run. Default of <c>0</c> treats any soak-level
/// transport fault as a stress failure — the wrapping transport intentionally never
/// reports drops as soak faults, so a non-zero count here implies a real degraded
/// behaviour outside the injection layer.</param>
public sealed record AlphaStressFaultInjectionTolerance(
    double MaxDropRate,
    int MaxObservedSoakIssues)
{
    /// <summary>Maximum fraction of inbound <c>Received</c> events the wrapping
    /// transport is allowed to drop across the run. Range <c>[0,1]</c>. Default of
    /// <c>1.0</c> effectively disables the inbound check so the legacy M11.1 send-only
    /// shape keeps the same pass/fail behaviour; tighten when the stress profile
    /// actively injects inbound loss via
    /// <see cref="Opus.Engine.Net.Transport.LatencyLossInjectionProfile.InboundLossRate"/>.</summary>
    public double MaxInboundDropRate { get; init; } = 1.0;

    /// <summary>Default tolerance — 25% outbound drop ceiling, inbound check disabled,
    /// zero non-injection soak issues tolerated. Sized so the default
    /// <see cref="AlphaStressNetworkProfile"/> passes on healthy alpha hardware;
    /// tighten when a stress profile actively probes the game-side jitter
    /// envelope.</summary>
    public static AlphaStressFaultInjectionTolerance Default { get; } = new(
        MaxDropRate: 0.25,
        MaxObservedSoakIssues: 0);

    /// <summary>Throws when the tolerance is internally inconsistent.</summary>
    public void Validate()
    {
        if (double.IsNaN(MaxDropRate) || MaxDropRate < 0.0 || MaxDropRate > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxDropRate),
                "MaxDropRate must be in the inclusive range [0, 1].");
        }

        if (double.IsNaN(MaxInboundDropRate) || MaxInboundDropRate < 0.0 || MaxInboundDropRate > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxInboundDropRate),
                "MaxInboundDropRate must be in the inclusive range [0, 1].");
        }

        if (MaxObservedSoakIssues < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxObservedSoakIssues),
                "MaxObservedSoakIssues must be non-negative.");
        }
    }
}
