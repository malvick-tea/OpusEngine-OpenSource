using System;

namespace Opus.Engine.AlphaStress.Network;

/// <summary>
/// One <see cref="IAlphaStressNetworkProbe"/> observation captured at the end of a
/// stress iteration. Carries the per-iteration counters the wrapping transport accumulated
/// plus the inner soak report's issue count. Pure data — the aggregator reads it once and
/// derives the run-wide <see cref="AlphaStressNetworkSummary"/> from a stream of these.
/// </summary>
/// <param name="IterationIndex">Zero-based iteration index the observation belongs to.
/// Must be non-negative.</param>
/// <param name="ObservedAtUtc">UTC timestamp the probe captured the observation at.</param>
/// <param name="ClientSendAttempts">Total number of <see cref="Opus.Net.Transport.INetTransport.Send"/>
/// calls every client transport saw during the iteration. The denominator for
/// <see cref="AlphaStressNetworkSummary.DropFraction"/>.</param>
/// <param name="DroppedPackets">Number of attempted sends the wrapping transport
/// silently dropped (loss injection).</param>
/// <param name="DelayedPackets">Number of attempted sends the wrapping transport queued
/// behind the configured added-latency deadline. Includes packets that ultimately landed
/// in the inner transport — this counter measures latency injection traffic shape, not
/// failure.</param>
/// <param name="SoakIssueCount">Number of <see cref="Opus.Engine.Net.Soak.NetSoakIssue"/>
/// observations the inner workload recorded; the wrapping transport never injects these
/// itself, so any non-zero value points at real degraded behaviour worth surfacing.</param>
public sealed record AlphaStressNetworkObservation(
    int IterationIndex,
    DateTimeOffset ObservedAtUtc,
    long ClientSendAttempts,
    long DroppedPackets,
    long DelayedPackets,
    int SoakIssueCount)
{
    /// <summary>Number of inbound <c>Received</c> events the wrapping transport
    /// observed before applying the inbound fault-injection filter. The denominator
    /// for <see cref="AlphaStressNetworkSummary.InboundDropFraction"/>.</summary>
    public long InboundAttempts { get; init; }

    /// <summary>Number of inbound <c>Received</c> events the wrapping transport
    /// dropped before surfacing to the caller.</summary>
    public long InboundDroppedPackets { get; init; }

    /// <summary>Number of inbound <c>Received</c> events the wrapping transport queued
    /// behind the configured inbound-latency deadline.</summary>
    public long InboundDelayedPackets { get; init; }

    /// <summary>Throws when the observation is internally inconsistent.</summary>
    public void Validate()
    {
        if (IterationIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(IterationIndex),
                "IterationIndex must be non-negative.");
        }

        if (ClientSendAttempts < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ClientSendAttempts),
                "ClientSendAttempts must be non-negative.");
        }

        if (DroppedPackets < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DroppedPackets),
                "DroppedPackets must be non-negative.");
        }

        if (DelayedPackets < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DelayedPackets),
                "DelayedPackets must be non-negative.");
        }

        if (SoakIssueCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SoakIssueCount),
                "SoakIssueCount must be non-negative.");
        }

        if (DroppedPackets + DelayedPackets > ClientSendAttempts)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DroppedPackets),
                "Dropped + delayed packets must not exceed total client send attempts.");
        }

        if (InboundAttempts < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(InboundAttempts),
                "InboundAttempts must be non-negative.");
        }

        if (InboundDroppedPackets < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(InboundDroppedPackets),
                "InboundDroppedPackets must be non-negative.");
        }

        if (InboundDelayedPackets < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(InboundDelayedPackets),
                "InboundDelayedPackets must be non-negative.");
        }

        if (InboundDroppedPackets + InboundDelayedPackets > InboundAttempts)
        {
            throw new ArgumentOutOfRangeException(
                nameof(InboundDroppedPackets),
                "Inbound dropped + delayed packets must not exceed total inbound attempts.");
        }
    }
}
