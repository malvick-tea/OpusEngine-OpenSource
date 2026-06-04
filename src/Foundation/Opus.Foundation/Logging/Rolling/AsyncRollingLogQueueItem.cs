using System;
using System.Threading;

namespace Opus.Foundation;

/// <summary>
/// Discriminated union carried by the <see cref="AsyncRollingLogSink"/> worker queue.
/// Either an entry waiting to be written or a flush request waiting to be acknowledged.
/// Internal because the worker contract is an implementation detail of the sink.
/// </summary>
internal abstract class AsyncRollingLogQueueItem
{
    private protected AsyncRollingLogQueueItem()
    {
    }
}

/// <summary>Queued log entry awaiting write through the inner sink.</summary>
internal sealed class AsyncRollingLogEntryItem : AsyncRollingLogQueueItem
{
    public AsyncRollingLogEntryItem(RollingLogEntry entry)
    {
        Entry = entry;
    }

    public RollingLogEntry Entry { get; }
}

/// <summary>Flush request. The worker calls <see cref="IRollingLogSink.Flush"/> on the
/// inner sink and signals <see cref="Completion"/> when done so the caller can return.</summary>
internal sealed class AsyncRollingLogFlushItem : AsyncRollingLogQueueItem
{
    public AsyncRollingLogFlushItem(bool toDisk)
    {
        ToDisk = toDisk;
        Completion = new ManualResetEventSlim(initialState: false);
    }

    public bool ToDisk { get; }

    public ManualResetEventSlim Completion { get; }

    public Exception? Error { get; set; }
}
