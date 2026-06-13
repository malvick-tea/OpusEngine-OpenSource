using System;
using System.Threading;

namespace Opus.Foundation;

/// <summary>
/// Worker-thread half of <see cref="AsyncRollingLogSink"/>. Kept in a separate file so
/// the public producer-side surface in the main file stays inside the production source
/// cap and so the worker / fsync responsibilities read as one focused unit.
/// </summary>
public sealed partial class AsyncRollingLogSink
{
    private void WorkerLoop()
    {
        try
        {
            foreach (var item in _queue.GetConsumingEnumerable(_shutdown.Token))
            {
                ProcessItem(item);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
            // The queue or shutdown source was disposed while the worker was parked in
            // GetConsumingEnumerable. The sink is being torn down; the worker exits
            // quietly. An unhandled exception here would kill the host process, which is
            // exactly the no-throw-from-logging-paths guarantee this sink promises.
        }
    }

    private void ProcessItem(AsyncRollingLogQueueItem item)
    {
        if (item is AsyncRollingLogEntryItem entryItem)
        {
            SafeInnerLogEntry(entryItem.Entry);
            Interlocked.Increment(ref _processed);
            MaybeFsyncAfterEntry(entryItem.Entry);
            return;
        }

        if (item is AsyncRollingLogFlushItem flushItem)
        {
            ProcessFlush(flushItem);
        }
    }

    private void ProcessFlush(AsyncRollingLogFlushItem flushItem)
    {
        try
        {
            _inner.Flush(flushItem.ToDisk);
            if (flushItem.ToDisk)
            {
                Interlocked.Increment(ref _fsynced);
                UpdateLastFsync();
            }
        }
        catch (Exception ex)
        {
            flushItem.Error = ex;
        }
        finally
        {
            flushItem.Completion.Set();
        }
    }

    private void SafeInnerLogEntry(RollingLogEntry entry)
    {
        try
        {
            _inner.LogEntry(entry);
        }
        catch
        {
            // Hostile inner sink cannot tear down the worker.
        }
    }

    private void MaybeFsyncAfterEntry(RollingLogEntry entry)
    {
        if (_options.FsyncOnCritical && entry.Level >= LogLevel.Critical)
        {
            TryFsyncInner();
            return;
        }

        if (_options.FsyncInterval is not { } interval)
        {
            return;
        }

        var lastTicks = Interlocked.Read(ref _lastFsyncTicks);
        var elapsed = _clock.GetUtcNow().UtcTicks - lastTicks;
        if (elapsed >= interval.Ticks)
        {
            TryFsyncInner();
        }
    }

    private void TryFsyncInner()
    {
        try
        {
            _inner.Flush(toDisk: true);
            Interlocked.Increment(ref _fsynced);
            UpdateLastFsync();
        }
        catch
        {
        }
    }

    private void UpdateLastFsync()
    {
        Interlocked.Exchange(ref _lastFsyncTicks, _clock.GetUtcNow().UtcTicks);
    }
}
