using System;
using System.Collections.Generic;

namespace Opus.Foundation;

/// <summary>
/// Log sink that retains a bounded tail and writes a session log that can be attached
/// to tester failure reports.
/// </summary>
public interface IRollingLogSink : ILog, IDisposable
{
    /// <summary>Path to the file currently receiving log entries.</summary>
    string CurrentLogFilePath { get; }

    /// <summary>Returns at most <paramref name="maxEntries"/> most-recent entries.</summary>
    IReadOnlyList<RollingLogEntry> SnapshotTail(int maxEntries);

    /// <summary>Writes a pre-built entry without re-stamping its UTC timestamp. Required
    /// by <see cref="AsyncRollingLogSink"/> so an entry queued on one thread keeps its
    /// accept-time timestamp when the worker thread eventually writes it. Disposed sinks
    /// must treat the call as a silent no-op.</summary>
    void LogEntry(RollingLogEntry entry);

    /// <summary>Synchronously flushes any internally-buffered entries to the next stage.
    /// When <paramref name="toDisk"/> is <c>true</c>, the sink additionally requests an
    /// OS-level durability flush (fsync / FlushFileBuffers) so the log survives a crash
    /// or power loss. Sinks that cannot enforce durability still honour the buffered
    /// flush. Disposed sinks must treat the call as a silent no-op.</summary>
    void Flush(bool toDisk);
}
