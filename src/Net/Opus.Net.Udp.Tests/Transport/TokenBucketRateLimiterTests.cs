using FluentAssertions;
using Opus.Net.Udp.Transport;
using Xunit;

namespace Opus.Net.Udp.Tests.Transport;

/// <summary>
/// Deterministic unit coverage for <see cref="TokenBucketRateLimiter"/>. Time enters only as an
/// explicit tick argument, so these tests drive refill precisely without sleeping or touching a
/// socket — the pure core of the per-peer inbound rate limiter proved independently of the UDP
/// transport that composes it.
/// </summary>
public sealed class TokenBucketRateLimiterTests
{
    private const long StartTicks = 1_000;

    [Fact]
    public void Starts_full_and_admits_a_whole_burst_at_once()
    {
        const int capacity = 4;
        var bucket = new TokenBucketRateLimiter(capacity, refillTokensPerSecond: 1, StartTicks);

        for (var i = 0; i < capacity; i++)
        {
            bucket.TryConsume(StartTicks).Should().BeTrue($"token {i} is within the starting burst");
        }

        bucket.TryConsume(StartTicks).Should().BeFalse("the burst is spent and no time has passed to refill");
    }

    [Fact]
    public void Refills_one_second_of_rate_after_the_burst_is_spent()
    {
        const int refillPerSecond = 8;
        var bucket = new TokenBucketRateLimiter(capacityTokens: 20, refillPerSecond, StartTicks);
        DrainCompletely(bucket, drainTicks: StartTicks);

        bucket.TryConsume(StartTicks).Should().BeFalse("the bucket is empty before any time advances");

        // One second later exactly refillPerSecond tokens are available (capacity 20 is not the
        // binding limit here, so this isolates the rate from the burst cap).
        var oneSecondLater = StartTicks + 1_000;
        for (var i = 0; i < refillPerSecond; i++)
        {
            bucket.TryConsume(oneSecondLater).Should().BeTrue($"refilled token {i} is within one second of rate");
        }

        bucket.TryConsume(oneSecondLater).Should().BeFalse("one second only refills the sustained rate, no more");
    }

    [Fact]
    public void Refill_is_proportional_to_elapsed_milliseconds()
    {
        const int refillPerSecond = 1_000; // one token per millisecond
        var bucket = new TokenBucketRateLimiter(capacityTokens: 20, refillPerSecond, StartTicks);
        DrainCompletely(bucket, drainTicks: StartTicks);

        var threeMillisLater = StartTicks + 3;
        for (var i = 0; i < 3; i++)
        {
            bucket.TryConsume(threeMillisLater).Should().BeTrue($"millisecond {i} refilled one token at 1000/s");
        }

        bucket.TryConsume(threeMillisLater).Should().BeFalse("three milliseconds at 1000/s refill exactly three tokens");
    }

    [Fact]
    public void Never_accumulates_beyond_capacity_however_long_it_idles()
    {
        const int capacity = 5;
        var bucket = new TokenBucketRateLimiter(capacity, refillTokensPerSecond: 1_000, StartTicks);
        DrainCompletely(bucket, drainTicks: StartTicks);

        // Idle far longer than it takes to refill capacity; the bucket must clamp, not over-fill.
        var muchLater = StartTicks + 10_000;
        for (var i = 0; i < capacity; i++)
        {
            bucket.TryConsume(muchLater).Should().BeTrue($"capacity token {i} is available after a long idle");
        }

        bucket.TryConsume(muchLater).Should().BeFalse("a long idle still tops out at the burst capacity");
    }

    [Fact]
    public void Does_not_refill_when_time_does_not_advance()
    {
        var bucket = new TokenBucketRateLimiter(capacityTokens: 3, refillTokensPerSecond: 1_000, StartTicks);
        DrainCompletely(bucket, drainTicks: StartTicks);

        bucket.TryConsume(StartTicks).Should().BeFalse("the same instant cannot refill even a fast bucket");
    }

    [Fact]
    public void Ignores_backward_time_without_corrupting_the_refill_baseline()
    {
        var bucket = new TokenBucketRateLimiter(capacityTokens: 3, refillTokensPerSecond: 1_000, StartTicks);
        DrainCompletely(bucket, drainTicks: StartTicks);

        bucket.TryConsume(StartTicks - 500).Should().BeFalse("a backward clock never refills");

        // The backward call must not have moved the baseline to StartTicks-500; one millisecond past
        // the real baseline still refills exactly one token, not five hundred and one.
        bucket.TryConsume(StartTicks + 1).Should().BeTrue("one millisecond past the true baseline refills one token");
        bucket.TryConsume(StartTicks + 1).Should().BeFalse("only one token was earned, so the second consume fails");
    }

    private static void DrainCompletely(TokenBucketRateLimiter bucket, long drainTicks)
    {
        // Take tokens until the bucket reports empty. The starting burst is finite, so this halts.
        while (bucket.TryConsume(drainTicks))
        {
        }
    }
}
