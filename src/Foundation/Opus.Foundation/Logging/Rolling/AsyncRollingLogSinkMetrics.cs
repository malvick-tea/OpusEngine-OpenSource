namespace Opus.Foundation;

/// <summary>
/// Point-in-time observability snapshot from <see cref="AsyncRollingLogSink"/>. The
/// counters are monotonically non-decreasing across the sink's lifetime; subtracting two
/// snapshots yields the delta over an interval.
/// </summary>
public readonly record struct AsyncRollingLogSinkMetrics(
    long Accepted,
    long Processed,
    long Dropped,
    long Fsynced,
    int QueueDepth,
    int QueueCapacity);
