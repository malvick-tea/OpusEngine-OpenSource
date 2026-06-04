namespace Opus.Engine.AlphaStress.Network;

/// <summary>
/// Aggregated network fault-injection summary across an entire stress run. Plain data
/// shape so the report writer can serialise it through <c>System.Text.Json</c> and so
/// tests can assert against the numbers without reaching into harness internals.
/// </summary>
/// <param name="IterationCount">Number of <see cref="AlphaStressNetworkObservation"/>s
/// the aggregator folded into the summary.</param>
/// <param name="TotalClientSendAttempts">Sum of
/// <see cref="AlphaStressNetworkObservation.ClientSendAttempts"/> across the run. The
/// denominator for <see cref="DropFraction"/> and <see cref="DelayedFraction"/>.</param>
/// <param name="TotalDroppedPackets">Sum of dropped packets across the run.</param>
/// <param name="TotalDelayedPackets">Sum of delayed (queued behind injected latency)
/// packets across the run.</param>
/// <param name="TotalSoakIssueCount">Sum of inner soak issue counts across the run.</param>
/// <param name="DropFraction"><c>TotalDroppedPackets / TotalClientSendAttempts</c>;
/// <c>0</c> when no attempts were observed.</param>
/// <param name="DelayedFraction"><c>TotalDelayedPackets / TotalClientSendAttempts</c>;
/// <c>0</c> when no attempts were observed.</param>
public sealed record AlphaStressNetworkSummary(
    int IterationCount,
    long TotalClientSendAttempts,
    long TotalDroppedPackets,
    long TotalDelayedPackets,
    int TotalSoakIssueCount,
    double DropFraction,
    double DelayedFraction)
{
    /// <summary>Sum of inbound <c>Received</c> events the wrapping transport observed
    /// before filtering. Denominator for <see cref="InboundDropFraction"/>.</summary>
    public long TotalInboundAttempts { get; init; }

    /// <summary>Sum of inbound <c>Received</c> events the wrapping transport dropped
    /// before surfacing.</summary>
    public long TotalInboundDroppedPackets { get; init; }

    /// <summary>Sum of inbound <c>Received</c> events the wrapping transport queued
    /// behind the configured inbound-latency deadline.</summary>
    public long TotalInboundDelayedPackets { get; init; }

    /// <summary><c>TotalInboundDroppedPackets / TotalInboundAttempts</c>; <c>0</c>
    /// when no inbound attempts were observed.</summary>
    public double InboundDropFraction { get; init; }

    /// <summary><c>TotalInboundDelayedPackets / TotalInboundAttempts</c>; <c>0</c>
    /// when no inbound attempts were observed.</summary>
    public double InboundDelayedFraction { get; init; }

    /// <summary>Empty summary — emitted when the aggregator observed zero iterations.</summary>
    public static AlphaStressNetworkSummary Empty { get; } = new(
        IterationCount: 0,
        TotalClientSendAttempts: 0,
        TotalDroppedPackets: 0,
        TotalDelayedPackets: 0,
        TotalSoakIssueCount: 0,
        DropFraction: 0.0,
        DelayedFraction: 0.0);

    /// <summary>True when at least one observation was folded into the summary.</summary>
    public bool HasObservations => IterationCount > 0;
}
