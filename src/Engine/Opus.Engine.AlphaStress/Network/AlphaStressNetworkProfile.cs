using System;
using Opus.Engine.Net.Soak;
using Opus.Engine.Net.Transport;

namespace Opus.Engine.AlphaStress.Network;

/// <summary>
/// Per-run knobs for the stress harness's network probe. Wraps the deterministic
/// <see cref="LatencyLossInjectionProfile"/> applied to the loopback transports, the
/// underlying <see cref="NetSoakProfile"/> that drives the workload, and the
/// <see cref="AlphaStressFaultInjectionTolerance"/> that decides whether the observed
/// degradation breaches the alpha bar. Engine-neutral data; the probe reads the record
/// once and never mutates it.
/// </summary>
/// <param name="Injection">Deterministic loss + latency injection profile applied to
/// each client transport.</param>
/// <param name="Soak">Underlying soak workload — peer count, packets per peer, payload
/// shape, connect/workload budgets, echo policy.</param>
/// <param name="Tolerance">Pass/fail thresholds applied against the aggregated
/// <see cref="AlphaStressNetworkSummary"/>.</param>
public sealed record AlphaStressNetworkProfile(
    LatencyLossInjectionProfile Injection,
    NetSoakProfile Soak,
    AlphaStressFaultInjectionTolerance Tolerance)
{
    /// <summary>Default canonical network profile: 4-peer cohort echoing 32 256-byte
    /// payloads each through a 10% loss / 5 ms added-latency injection layer with the
    /// default 25% drop tolerance. Sized so a single iteration completes inside the
    /// per-iteration wall budget on loopback hardware without producing flaky CI
    /// noise.</summary>
    public static AlphaStressNetworkProfile Default { get; } = new(
        Injection: new LatencyLossInjectionProfile(
            LossRate: 0.10,
            AddedLatency: TimeSpan.FromMilliseconds(5),
            Seed: 20260527),
        Soak: NetSoakProfile.Default with { PacketsPerPeer = 32 },
        Tolerance: AlphaStressFaultInjectionTolerance.Default);

    /// <summary>Throws when the profile is internally inconsistent.</summary>
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Injection);
        ArgumentNullException.ThrowIfNull(Soak);
        ArgumentNullException.ThrowIfNull(Tolerance);
        Injection.Validate();
        Soak.Validate();
        Tolerance.Validate();
    }
}
