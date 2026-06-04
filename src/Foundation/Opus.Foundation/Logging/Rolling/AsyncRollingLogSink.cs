using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Opus.Foundation;

/// <summary>
/// Off-thread decorator around an inner <see cref="IRollingLogSink"/>. The producer
/// thread enqueues a pre-built <see cref="RollingLogEntry"/> and returns; a single worker
/// thread drains the bounded queue into the inner sink so disk IO does not stall the
/// caller. Overflow is governed by an explicit
/// <see cref="RollingLogBackpressurePolicy"/>; durability is governed by a periodic
/// fsync interval plus an opt-in flush on <see cref="LogLevel.Critical"/> entries.
/// </summary>
/// <remarks>
/// <para>The sink owns its inner — disposing the async wrapper also disposes the inner.
/// Producer threads are protected from inner-sink failures: every inner call is wrapped
/// so an exception cannot escape into the logging hot path. After <see cref="Dispose"/>
/// the sink becomes a silent no-op for subsequent <see cref="Log"/> /
/// <see cref="LogEntry"/> / <see cref="Flush"/> calls so a host-level catch handler
/// logging into a torn-down composite cannot itself crash.</para>
/// </remarks>
public sealed partial class AsyncRollingLogSink : IRollingLogSink
{
    private readonly IRollingLogSink _inner;
    private readonly AsyncRollingLogSinkOptions _options;
    private readonly TimeProvider _clock;
    private readonly BlockingCollection<AsyncRollingLogQueueItem> _queue;
    private readonly object _tailLock = new();
    private readonly Queue<RollingLogEntry> _tail;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Thread _worker;
    private long _accepted;
    private long _processed;
    private long _dropped;
    private long _fsynced;
    private long _lastFsyncTicks;
    private volatile bool _disposed;

    /// <summary>Opens an async rolling sink around an inner sink with default clock.</summary>
    public AsyncRollingLogSink(IRollingLogSink inner, AsyncRollingLogSinkOptions options)
        : this(inner, options, TimeProvider.System)
    {
    }

    /// <summary>Test-friendly constructor with an explicit clock.</summary>
    public AsyncRollingLogSink(IRollingLogSink inner, AsyncRollingLogSinkOptions options, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        options.Validate();

        _inner = inner;
        _options = options;
        _clock = clock;
        _queue = new BlockingCollection<AsyncRollingLogQueueItem>(
            new ConcurrentQueue<AsyncRollingLogQueueItem>(),
            options.QueueCapacity);
        _tail = new Queue<RollingLogEntry>(options.MaxTailEntries);
        _lastFsyncTicks = clock.GetUtcNow().UtcTicks;

        _worker = new Thread(WorkerLoop)
        {
            Name = options.WorkerThreadName,
            IsBackground = true,
        };
        _worker.Start();
    }

    /// <inheritdoc />
    public string CurrentLogFilePath => _inner.CurrentLogFilePath;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel level) => !_disposed && _inner.IsEnabled(level);

    /// <summary>Lock-free snapshot of writer-thread observability counters. Safe to call
    /// after <see cref="Dispose"/>: the queue-depth read degrades to 0 rather than throwing
    /// once the underlying queue is gone, preserving the no-throw contract for diagnostics
    /// readers (e.g. a failure-report capture during teardown).</summary>
    public AsyncRollingLogSinkMetrics SnapshotMetrics() => new(
        Accepted: Interlocked.Read(ref _accepted),
        Processed: Interlocked.Read(ref _processed),
        Dropped: Interlocked.Read(ref _dropped),
        Fsynced: Interlocked.Read(ref _fsynced),
        QueueDepth: ReadQueueDepth(),
        QueueCapacity: _options.QueueCapacity);

    private int ReadQueueDepth()
    {
        if (_disposed)
        {
            return 0;
        }

        try
        {
            return _queue.Count;
        }
        catch (ObjectDisposedException)
        {
            return 0;
        }
    }

    /// <inheritdoc />
    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        if (_disposed)
        {
            return;
        }

        if (!_inner.IsEnabled(level))
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(message);
        var entry = RollingLogEntry.Create(_clock.GetUtcNow(), level, message, exception);
        if (TryEnqueueEntry(entry))
        {
            Retain(entry);
            Interlocked.Increment(ref _accepted);
        }
    }

    /// <inheritdoc />
    public void LogEntry(RollingLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (_disposed)
        {
            return;
        }

        if (!_inner.IsEnabled(entry.Level))
        {
            return;
        }

        if (TryEnqueueEntry(entry))
        {
            Retain(entry);
            Interlocked.Increment(ref _accepted);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<RollingLogEntry> SnapshotTail(int maxEntries)
    {
        if (maxEntries < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxEntries), "maxEntries must be at least 1.");
        }

        lock (_tailLock)
        {
            var available = _tail.Count;
            if (available == 0)
            {
                return Array.Empty<RollingLogEntry>();
            }

            var take = Math.Min(maxEntries, available);
            var skip = available - take;
            var result = new RollingLogEntry[take];
            var index = 0;
            foreach (var stored in _tail)
            {
                if (index >= skip)
                {
                    result[index - skip] = stored;
                }

                index++;
            }

            return result;
        }
    }

    /// <inheritdoc />
    public void Flush(bool toDisk)
    {
        if (_disposed)
        {
            return;
        }

        var item = new AsyncRollingLogFlushItem(toDisk);
        try
        {
            _queue.Add(item, _shutdown.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (InvalidOperationException)
        {
            // Covers ObjectDisposedException (a subtype): the queue was completed or torn
            // down concurrently, so the flush request is dropped rather than thrown.
            return;
        }

        item.Completion.Wait(_options.DrainOnDisposeTimeout);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _queue.CompleteAdding();
        }
        catch (ObjectDisposedException)
        {
        }

        if (!_worker.Join(_options.DrainOnDisposeTimeout))
        {
            _shutdown.Cancel();

            // A worker stuck inside a hung inner sink (a dead disk mid-write, a wedged
            // network sink) does not observe the cancel — it is blocked in the inner call,
            // not on the queue. Give it one more bounded grace period and then abandon it
            // rather than pinning disposal forever; the alpha host must be able to shut down.
            // The worker is a background thread, so an abandoned one cannot keep the process
            // alive, and the inner dispose below makes its next inner write throw (swallowed).
            _worker.Join(_options.DrainOnDisposeTimeout);
        }

        ReleaseLeftoverFlushWaiters();

        try
        {
            _inner.Flush(toDisk: true);
        }
        catch
        {
        }

        _inner.Dispose();
        _queue.Dispose();
        _shutdown.Dispose();
    }

    private bool TryEnqueueEntry(RollingLogEntry entry)
    {
        var item = new AsyncRollingLogEntryItem(entry);
        switch (_options.BackpressurePolicy)
        {
            case RollingLogBackpressurePolicy.Block:
                return TryEnqueueBlocking(item);
            case RollingLogBackpressurePolicy.DropNewest:
                return TryEnqueueDropNewest(item);
            case RollingLogBackpressurePolicy.DropOldest:
                return TryEnqueueDropOldest(item);
            default:
                Interlocked.Increment(ref _dropped);
                return false;
        }
    }

    private bool TryEnqueueBlocking(AsyncRollingLogQueueItem item)
    {
        var timeoutMillis = ConvertToTimeoutMilliseconds(_options.BlockTimeout);
        try
        {
            if (_queue.TryAdd(item, timeoutMillis, _shutdown.Token))
            {
                return true;
            }

            Interlocked.Increment(ref _dropped);
            return false;
        }
        catch (OperationCanceledException)
        {
            Interlocked.Increment(ref _dropped);
            return false;
        }
        catch (InvalidOperationException)
        {
            // Includes ObjectDisposedException (a subtype) from a concurrent teardown.
            Interlocked.Increment(ref _dropped);
            return false;
        }
    }

    private static int ConvertToTimeoutMilliseconds(TimeSpan timeout)
    {
        if (timeout == Timeout.InfiniteTimeSpan)
        {
            return Timeout.Infinite;
        }

        var totalMillis = timeout.TotalMilliseconds;
        if (totalMillis <= 0)
        {
            return 0;
        }

        if (totalMillis >= int.MaxValue)
        {
            return int.MaxValue - 1;
        }

        return (int)totalMillis;
    }

    private bool TryEnqueueDropNewest(AsyncRollingLogQueueItem item)
    {
        try
        {
            if (_queue.TryAdd(item))
            {
                return true;
            }

            Interlocked.Increment(ref _dropped);
            return false;
        }
        catch (InvalidOperationException)
        {
            // Includes ObjectDisposedException (a subtype) from a concurrent teardown.
            Interlocked.Increment(ref _dropped);
            return false;
        }
    }

    private bool TryEnqueueDropOldest(AsyncRollingLogQueueItem item)
    {
        while (true)
        {
            try
            {
                if (_queue.TryAdd(item))
                {
                    return true;
                }

                if (_queue.TryTake(out var evicted))
                {
                    if (evicted is AsyncRollingLogEntryItem)
                    {
                        Interlocked.Increment(ref _dropped);
                    }
                    else if (evicted is AsyncRollingLogFlushItem flush)
                    {
                        flush.Completion.Set();
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // Includes ObjectDisposedException (a subtype): the queue was completed or
                // disposed mid-eviction, so the entry is dropped instead of thrown.
                Interlocked.Increment(ref _dropped);
                return false;
            }
        }
    }

    private void Retain(RollingLogEntry entry)
    {
        lock (_tailLock)
        {
            _tail.Enqueue(entry);
            while (_tail.Count > _options.MaxTailEntries)
            {
                _tail.Dequeue();
            }
        }
    }

    private void ReleaseLeftoverFlushWaiters()
    {
        while (_queue.TryTake(out var leftover))
        {
            if (leftover is AsyncRollingLogFlushItem flush)
            {
                flush.Completion.Set();
            }
        }
    }
}
