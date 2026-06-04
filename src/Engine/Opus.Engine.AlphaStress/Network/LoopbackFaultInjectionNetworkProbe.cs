using System;
using Opus.Engine.Net.Soak;

namespace Opus.Engine.AlphaStress.Network;

/// <summary>
/// Runtime <see cref="IAlphaStressNetworkProbe"/> implementation. Each
/// <see cref="RunIteration"/> call opens a fresh <see cref="FaultInjectionLoopbackSoakRig"/>,
/// drives the inner <see cref="NetSoakHarness"/> against the supplied
/// <see cref="AlphaStressNetworkProfile"/>, and disposes the rig before returning. Per-
/// iteration rig lifetime matches the iteration runner's per-iteration host lifetime —
/// the stress harness exercises teardown as part of the run.
/// </summary>
/// <remarks>
/// The probe never throws past its public surface. Construction failures, soak workload
/// faults, and disposal exceptions all translate to observations carrying a non-zero
/// <see cref="AlphaStressNetworkObservation.SoakIssueCount"/> so the harness surfaces
/// them through <c>OPDX-STR-006</c> without crashing the run.
/// </remarks>
public sealed class LoopbackFaultInjectionNetworkProbe : IAlphaStressNetworkProbe
{
    private readonly TimeProvider _time;
    private bool _disposed;

    /// <summary>Creates a probe that uses the supplied <paramref name="time"/> for both
    /// the wrapping transport's latency scheduling and the soak harness's clock.</summary>
    public LoopbackFaultInjectionNetworkProbe(TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public AlphaStressNetworkObservation RunIteration(int iterationIndex, AlphaStressNetworkProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ObjectDisposedException.ThrowIf(_disposed, this);
        profile.Validate();
        if (iterationIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(iterationIndex),
                "iterationIndex must be non-negative.");
        }

        FaultInjectionLoopbackSoakRig? rig = null;
        try
        {
            rig = FaultInjectionLoopbackSoakRig.Create(profile.Soak.PeerCount, profile.Injection, _time);
            var report = NetSoakHarness.Run(profile.Soak, rig, _time);
            return new AlphaStressNetworkObservation(
                IterationIndex: iterationIndex,
                ObservedAtUtc: _time.GetUtcNow(),
                ClientSendAttempts: ComputeClientSendAttempts(profile),
                DroppedPackets: rig.TotalDroppedPackets,
                DelayedPackets: rig.TotalDelayedPackets,
                SoakIssueCount: report.Issues.Count)
            {
                InboundAttempts = rig.TotalInboundAttempts,
                InboundDroppedPackets = rig.TotalInboundDroppedPackets,
                InboundDelayedPackets = rig.TotalInboundDelayedPackets,
            };
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            return new AlphaStressNetworkObservation(
                IterationIndex: iterationIndex,
                ObservedAtUtc: _time.GetUtcNow(),
                ClientSendAttempts: ComputeClientSendAttempts(profile),
                DroppedPackets: 0,
                DelayedPackets: 0,
                SoakIssueCount: 1);
        }
        finally
        {
            rig?.Dispose();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _disposed = true;

    private static long ComputeClientSendAttempts(AlphaStressNetworkProfile profile) =>
        (long)profile.Soak.PeerCount * profile.Soak.PacketsPerPeer;
}
