namespace Opus.Foundation;

/// <summary>
/// Policy applied by <see cref="AsyncRollingLogSink"/> when the bounded write queue is
/// already full and a new entry arrives.
/// </summary>
public enum RollingLogBackpressurePolicy
{
    /// <summary>Discard the oldest queued entry so the new one can be admitted. Best
    /// default for crash diagnostics: the freshest log lines that reach the sink during
    /// a fault window are the most valuable.</summary>
    DropOldest = 0,

    /// <summary>Discard the new entry. Cheaper than <see cref="DropOldest"/> because no
    /// existing entry has to be evicted, but it favours stale history over recent events.</summary>
    DropNewest = 1,

    /// <summary>Block the calling thread until either a slot frees up or
    /// <see cref="AsyncRollingLogSinkOptions.BlockTimeout"/> elapses. Useful in
    /// throughput-sensitive runs where lost lines are unacceptable, but at the cost of
    /// back-pressuring whatever thread emitted the log.</summary>
    Block = 2,
}
