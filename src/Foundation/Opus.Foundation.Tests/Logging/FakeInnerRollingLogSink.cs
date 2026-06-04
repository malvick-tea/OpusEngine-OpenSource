using System;
using System.Collections.Generic;
using System.Threading;
using Opus.Foundation;

namespace Opus.Foundation.Tests.Logging;

/// <summary>
/// Test double for <see cref="IRollingLogSink"/> used by <see cref="AsyncRollingLogSinkTests"/>.
/// Records every <see cref="LogEntry"/> and <see cref="Flush"/> call so the async wrapper
/// can be observed from outside, and exposes knobs to simulate hostile inner behaviour
/// (throwing on log, hanging on flush, refusing levels).
/// </summary>
internal sealed class FakeInnerRollingLogSink : IRollingLogSink
{
    private readonly object _lock = new();
    private readonly List<RollingLogEntry> _written = new();
    private readonly Queue<RollingLogEntry> _tail = new();
    private int _bufferedFlushes;
    private int _diskFlushes;
    private int _disposeCount;

    public string CurrentLogFilePath { get; init; } = "/fake/opus.log";

    public LogLevel MinimumLevel { get; init; } = LogLevel.Trace;

    public int MaxTailEntries { get; init; } = 64;

    public Exception? ThrowOnLog { get; set; }

    public Exception? ThrowOnFlush { get; set; }

    public TimeSpan FlushDelay { get; set; }

    public ManualResetEventSlim? GateOnLog { get; set; }

    public IReadOnlyList<RollingLogEntry> WrittenEntries
    {
        get
        {
            lock (_lock)
            {
                return _written.ToArray();
            }
        }
    }

    public int BufferedFlushCount => Volatile.Read(ref _bufferedFlushes);

    public int DiskFlushCount => Volatile.Read(ref _diskFlushes);

    public int DisposeCount => Volatile.Read(ref _disposeCount);

    public bool IsEnabled(LogLevel level) => level != LogLevel.None && level >= MinimumLevel;

    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        if (!IsEnabled(level))
        {
            return;
        }

        LogEntry(RollingLogEntry.Create(DateTimeOffset.UtcNow, level, message, exception));
    }

    public void LogEntry(RollingLogEntry entry)
    {
        GateOnLog?.Wait();

        if (ThrowOnLog is not null)
        {
            throw ThrowOnLog;
        }

        if (!IsEnabled(entry.Level))
        {
            return;
        }

        lock (_lock)
        {
            _written.Add(entry);
            _tail.Enqueue(entry);
            while (_tail.Count > MaxTailEntries)
            {
                _tail.Dequeue();
            }
        }
    }

    public IReadOnlyList<RollingLogEntry> SnapshotTail(int maxEntries)
    {
        lock (_lock)
        {
            var take = Math.Min(maxEntries, _tail.Count);
            var skip = _tail.Count - take;
            var result = new RollingLogEntry[take];
            var i = 0;
            foreach (var entry in _tail)
            {
                if (i >= skip)
                {
                    result[i - skip] = entry;
                }

                i++;
            }

            return result;
        }
    }

    public void Flush(bool toDisk)
    {
        if (FlushDelay > TimeSpan.Zero)
        {
            Thread.Sleep(FlushDelay);
        }

        if (ThrowOnFlush is not null)
        {
            throw ThrowOnFlush;
        }

        if (toDisk)
        {
            Interlocked.Increment(ref _diskFlushes);
        }
        else
        {
            Interlocked.Increment(ref _bufferedFlushes);
        }
    }

    public void Dispose()
    {
        Interlocked.Increment(ref _disposeCount);
    }
}
