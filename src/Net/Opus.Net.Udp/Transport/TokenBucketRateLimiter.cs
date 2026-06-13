using System;

namespace Opus.Net.Udp.Transport;

/// <summary>
/// A monotonic token-bucket rate limiter used by <see cref="UdpServerTransport"/> to bound how
/// fast a single peer may enqueue inbound payloads. The bucket starts full (a freshly connected
/// peer gets its whole burst at once), refills continuously at a fixed sustained rate up to a
/// burst capacity, and hands out one token per accepted payload.
/// </summary>
/// <remarks>
/// <para>
/// Deterministic by construction: the caller supplies the monotonic timestamp
/// (<see cref="Environment.TickCount64"/> in production), so the type carries no wall clock and
/// unit tests drive refill with explicit ticks instead of sleeping. The engine rule against
/// wall-clock time inside deterministic code is honoured: time enters only as a parameter.
/// </para>
/// <para>
/// Not thread-safe by design. The server consumes tokens only from its single receive worker
/// (<c>UdpServerTransport.HandlePayload</c>), so the bucket is mutated by exactly one thread and
/// needs no lock; bolting on synchronisation would be defensive paranoia between collaborators
/// that already have a single-writer contract.
/// </para>
/// </remarks>
internal sealed class TokenBucketRateLimiter
{
    private const int MillisecondsPerSecond = 1000;
    private const double SingleToken = 1.0;

    private readonly double _capacityTokens;
    private readonly double _refillTokensPerMillisecond;
    private double _availableTokens;
    private long _lastRefillTicks;

    /// <param name="capacityTokens">Burst capacity: the most tokens the bucket ever holds, and the
    /// number of back-to-back payloads a peer may enqueue after an idle period. Must be at least 1;
    /// callers validate the configured value through <see cref="UdpTransportOptions.Validate"/>.</param>
    /// <param name="refillTokensPerSecond">Sustained refill rate in tokens per second — the long-run
    /// ceiling on payloads a peer may enqueue once its burst is spent.</param>
    /// <param name="nowTicks">The monotonic millisecond timestamp at construction, used as the
    /// refill baseline.</param>
    public TokenBucketRateLimiter(int capacityTokens, int refillTokensPerSecond, long nowTicks)
    {
        _capacityTokens = capacityTokens;
        _refillTokensPerMillisecond = refillTokensPerSecond / (double)MillisecondsPerSecond;
        _availableTokens = capacityTokens;
        _lastRefillTicks = nowTicks;
    }

    /// <summary>Refills the bucket for the time elapsed since the last call, then consumes a single
    /// token if one is available. Returns <c>true</c> when a token was taken (the payload is
    /// admitted) and <c>false</c> when the bucket is empty (the payload is shed).</summary>
    public bool TryConsume(long nowTicks)
    {
        Refill(nowTicks);
        if (_availableTokens >= SingleToken)
        {
            _availableTokens -= SingleToken;
            return true;
        }

        return false;
    }

    private void Refill(long nowTicks)
    {
        if (nowTicks <= _lastRefillTicks)
        {
            // No time has advanced (or the supplied clock moved backwards, which a monotonic source
            // never does). Either way there is nothing to add, and never anything to remove.
            return;
        }

        var elapsedMilliseconds = nowTicks - _lastRefillTicks;
        _lastRefillTicks = nowTicks;
        var refilledTokens = elapsedMilliseconds * _refillTokensPerMillisecond;
        _availableTokens = Math.Min(_capacityTokens, _availableTokens + refilledTokens);
    }
}
