using System;

namespace Opus.Foundation;

/// <summary>
/// Configuration for <see cref="AsyncRollingLogSink"/>. Values are validated up-front
/// (<see cref="Validate"/>) before the worker thread starts, so a misconfigured host
/// fails fast instead of half-creating an unobservable sink.
/// </summary>
public sealed record AsyncRollingLogSinkOptions(
    int QueueCapacity,
    RollingLogBackpressurePolicy BackpressurePolicy,
    TimeSpan BlockTimeout,
    TimeSpan? FsyncInterval,
    bool FsyncOnCritical,
    TimeSpan DrainOnDisposeTimeout,
    int MaxTailEntries,
    string WorkerThreadName)
{
    /// <summary>Default bounded-queue capacity (entries).</summary>
    public const int DefaultQueueCapacity = 4096;

    /// <summary>Smallest queue capacity the sink will accept. Smaller values cripple the
    /// worker thread because a single burst always overflows.</summary>
    public const int MinimumQueueCapacity = 16;

    /// <summary>Default in-memory tail size visible through <see cref="IRollingLogSink.SnapshotTail"/>.</summary>
    public const int DefaultMaxTailEntries = 256;

    /// <summary>Default block timeout for the <see cref="RollingLogBackpressurePolicy.Block"/>
    /// policy. After this, the entry is dropped rather than stalling the producer further.</summary>
    public static readonly TimeSpan DefaultBlockTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Default periodic fsync interval. Keeps the durability window short without
    /// flushing every entry.</summary>
    public static readonly TimeSpan DefaultFsyncInterval = TimeSpan.FromSeconds(5);

    /// <summary>Default drain-on-dispose timeout. The worker is given this long to finish
    /// the backlog before the sink abandons whatever remains.</summary>
    public static readonly TimeSpan DefaultDrainOnDisposeTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Default OS thread name for the writer worker. Visible in dumps.</summary>
    public const string DefaultWorkerThreadName = "Opus.AsyncRollingLog";

    /// <summary>Runtime defaults aimed at alpha tester hosts.</summary>
    public static AsyncRollingLogSinkOptions Default => new(
        QueueCapacity: DefaultQueueCapacity,
        BackpressurePolicy: RollingLogBackpressurePolicy.DropOldest,
        BlockTimeout: DefaultBlockTimeout,
        FsyncInterval: DefaultFsyncInterval,
        FsyncOnCritical: true,
        DrainOnDisposeTimeout: DefaultDrainOnDisposeTimeout,
        MaxTailEntries: DefaultMaxTailEntries,
        WorkerThreadName: DefaultWorkerThreadName);

    /// <summary>Validates option values before allocating queue / worker resources.</summary>
    public void Validate()
    {
        if (QueueCapacity < MinimumQueueCapacity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(QueueCapacity),
                $"QueueCapacity must be at least {MinimumQueueCapacity}.");
        }

        if (!Enum.IsDefined(typeof(RollingLogBackpressurePolicy), BackpressurePolicy))
        {
            throw new ArgumentOutOfRangeException(
                nameof(BackpressurePolicy),
                $"Unknown backpressure policy '{BackpressurePolicy}'.");
        }

        if (BlockTimeout < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(BlockTimeout),
                "BlockTimeout must not be negative.");
        }

        if (FsyncInterval is { } interval && interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(FsyncInterval),
                "FsyncInterval must be positive when set; pass null to disable periodic fsync.");
        }

        if (DrainOnDisposeTimeout < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DrainOnDisposeTimeout),
                "DrainOnDisposeTimeout must not be negative.");
        }

        if (MaxTailEntries < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxTailEntries),
                "MaxTailEntries must be at least 1.");
        }

        if (string.IsNullOrWhiteSpace(WorkerThreadName))
        {
            throw new ArgumentException(
                "WorkerThreadName must not be empty.",
                nameof(WorkerThreadName));
        }
    }
}
