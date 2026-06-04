using System;

namespace Opus.Engine.AlphaStress.Network;

/// <summary>
/// Streaming aggregator for <see cref="AlphaStressNetworkObservation"/>. Folds
/// per-iteration network counters into a run-wide
/// <see cref="AlphaStressNetworkSummary"/>. Thread-affinity: not thread-safe — the
/// stress harness owns a per-run aggregator and consumes it on the orchestration
/// thread.
/// </summary>
public sealed class AlphaStressNetworkAggregator
{
    private int _iterationCount;
    private long _totalSendAttempts;
    private long _totalDropped;
    private long _totalDelayed;
    private int _totalSoakIssues;
    private long _totalInboundAttempts;
    private long _totalInboundDropped;
    private long _totalInboundDelayed;
    private int _lastIterationIndex = -1;

    /// <summary>Records one iteration observation. Rejects out-of-order iteration
    /// indices loudly so a caller bug (double-fire, missed reset) surfaces immediately
    /// instead of silently producing duplicated fractions.</summary>
    public void Record(AlphaStressNetworkObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        observation.Validate();
        if (observation.IterationIndex <= _lastIterationIndex)
        {
            throw new ArgumentOutOfRangeException(
                nameof(observation),
                $"IterationIndex must move forward across consecutive observations; saw {observation.IterationIndex} after {_lastIterationIndex}.");
        }

        _iterationCount++;
        _totalSendAttempts += observation.ClientSendAttempts;
        _totalDropped += observation.DroppedPackets;
        _totalDelayed += observation.DelayedPackets;
        _totalSoakIssues += observation.SoakIssueCount;
        _totalInboundAttempts += observation.InboundAttempts;
        _totalInboundDropped += observation.InboundDroppedPackets;
        _totalInboundDelayed += observation.InboundDelayedPackets;
        _lastIterationIndex = observation.IterationIndex;
    }

    /// <summary>Builds the aggregated summary. Returns
    /// <see cref="AlphaStressNetworkSummary.Empty"/> when no observations were
    /// recorded.</summary>
    public AlphaStressNetworkSummary BuildSummary()
    {
        if (_iterationCount == 0)
        {
            return AlphaStressNetworkSummary.Empty;
        }

        var dropFraction = _totalSendAttempts == 0
            ? 0.0
            : (double)_totalDropped / _totalSendAttempts;
        var delayedFraction = _totalSendAttempts == 0
            ? 0.0
            : (double)_totalDelayed / _totalSendAttempts;
        var inboundDropFraction = _totalInboundAttempts == 0
            ? 0.0
            : (double)_totalInboundDropped / _totalInboundAttempts;
        var inboundDelayedFraction = _totalInboundAttempts == 0
            ? 0.0
            : (double)_totalInboundDelayed / _totalInboundAttempts;

        return new AlphaStressNetworkSummary(
            IterationCount: _iterationCount,
            TotalClientSendAttempts: _totalSendAttempts,
            TotalDroppedPackets: _totalDropped,
            TotalDelayedPackets: _totalDelayed,
            TotalSoakIssueCount: _totalSoakIssues,
            DropFraction: dropFraction,
            DelayedFraction: delayedFraction)
        {
            TotalInboundAttempts = _totalInboundAttempts,
            TotalInboundDroppedPackets = _totalInboundDropped,
            TotalInboundDelayedPackets = _totalInboundDelayed,
            InboundDropFraction = inboundDropFraction,
            InboundDelayedFraction = inboundDelayedFraction,
        };
    }
}
