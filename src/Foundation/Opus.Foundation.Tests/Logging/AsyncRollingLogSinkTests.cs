using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Opus.Foundation;
using Xunit;

namespace Opus.Foundation.Tests.Logging;

public sealed class AsyncRollingLogSinkTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(2);

    [Fact]
    public void Worker_writes_accepted_entries_to_inner_in_order()
    {
        var inner = new FakeInnerRollingLogSink();
        using var sink = new AsyncRollingLogSink(inner, BasicOptions());

        sink.Info("first");
        sink.Info("second");
        sink.Info("third");
        sink.Flush(toDisk: false);

        var messages = inner.WrittenEntries.Select(e => e.Message).ToArray();
        messages.Should().Equal("first", "second", "third");
    }

    [Fact]
    public void Snapshot_tail_returns_most_recent_accepted_entries()
    {
        var inner = new FakeInnerRollingLogSink();
        using var sink = new AsyncRollingLogSink(inner, BasicOptions() with { MaxTailEntries = 2 });

        sink.Info("oldest");
        sink.Info("middle");
        sink.Info("newest");
        sink.Flush(toDisk: false);

        var tail = sink.SnapshotTail(8).Select(e => e.Message).ToArray();
        tail.Should().Equal("middle", "newest");
    }

    [Fact]
    public void Current_log_file_path_proxies_inner()
    {
        var inner = new FakeInnerRollingLogSink { CurrentLogFilePath = "/proxy/path.log" };
        using var sink = new AsyncRollingLogSink(inner, BasicOptions());

        sink.CurrentLogFilePath.Should().Be("/proxy/path.log");
    }

    [Fact]
    public void Is_enabled_returns_false_after_dispose()
    {
        var inner = new FakeInnerRollingLogSink();
        var sink = new AsyncRollingLogSink(inner, BasicOptions());

        sink.IsEnabled(LogLevel.Information).Should().BeTrue();
        sink.Dispose();

        sink.IsEnabled(LogLevel.Information).Should().BeFalse();
    }

    [Fact]
    public void Log_after_dispose_is_silent_no_op()
    {
        var inner = new FakeInnerRollingLogSink();
        var sink = new AsyncRollingLogSink(inner, BasicOptions());
        sink.Info("before");
        sink.Flush(toDisk: false);
        sink.Dispose();

        var act = () => sink.Info("after");

        act.Should().NotThrow();
        inner.WrittenEntries.Should().ContainSingle(e => e.Message == "before");
    }

    [Fact]
    public void Worker_survives_inner_throw_on_log()
    {
        var inner = new FakeInnerRollingLogSink { ThrowOnLog = new InvalidOperationException("boom") };
        using var sink = new AsyncRollingLogSink(inner, BasicOptions());

        sink.Info("ignored-because-inner-throws");
        sink.Flush(toDisk: false);

        inner.ThrowOnLog = null;
        sink.Info("survives");
        sink.Flush(toDisk: false);

        inner.WrittenEntries.Should().ContainSingle(e => e.Message == "survives");
    }

    [Fact]
    public void Dispose_disposes_inner_exactly_once()
    {
        var inner = new FakeInnerRollingLogSink();
        var sink = new AsyncRollingLogSink(inner, BasicOptions());

        sink.Dispose();
        sink.Dispose();

        inner.DisposeCount.Should().Be(1);
    }

    [Fact]
    public void Flush_without_disk_calls_inner_buffered_flush()
    {
        var inner = new FakeInnerRollingLogSink();
        using var sink = new AsyncRollingLogSink(
            inner,
            BasicOptions() with { FsyncInterval = null, FsyncOnCritical = false });
        sink.Info("payload");

        sink.Flush(toDisk: false);

        inner.BufferedFlushCount.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public void Flush_with_disk_increments_fsync_counter()
    {
        var inner = new FakeInnerRollingLogSink();
        using var sink = new AsyncRollingLogSink(
            inner,
            BasicOptions() with { FsyncInterval = null, FsyncOnCritical = false });
        sink.Info("payload");

        sink.Flush(toDisk: true);

        sink.SnapshotMetrics().Fsynced.Should().BeGreaterOrEqualTo(1);
        inner.DiskFlushCount.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public void Critical_entry_triggers_inner_fsync_when_policy_enabled()
    {
        var inner = new FakeInnerRollingLogSink();
        using var sink = new AsyncRollingLogSink(
            inner,
            BasicOptions() with { FsyncOnCritical = true, FsyncInterval = null });

        sink.Critical("system in trouble");
        WaitFor(() => inner.DiskFlushCount >= 1);

        inner.DiskFlushCount.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public void Periodic_fsync_runs_after_interval_elapses()
    {
        var inner = new FakeInnerRollingLogSink();
        using var sink = new AsyncRollingLogSink(
            inner,
            BasicOptions() with
            {
                FsyncInterval = TimeSpan.FromMilliseconds(10),
                FsyncOnCritical = false,
            });

        sink.Info("warmup");
        sink.Flush(toDisk: false);
        var baselineDiskFlushes = inner.DiskFlushCount;
        Thread.Sleep(30);
        sink.Info("after-interval");
        WaitFor(() => inner.DiskFlushCount > baselineDiskFlushes);

        inner.DiskFlushCount.Should().BeGreaterThan(baselineDiskFlushes);
    }

    [Fact]
    public void Log_entry_preserves_accept_time_timestamp_into_inner()
    {
        var inner = new FakeInnerRollingLogSink();
        using var sink = new AsyncRollingLogSink(inner, BasicOptions());
        var stamp = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var entry = RollingLogEntry.Create(stamp, LogLevel.Information, "queued", null);

        sink.LogEntry(entry);
        sink.Flush(toDisk: false);

        inner.WrittenEntries.Should().ContainSingle(e => e.TimestampUtc == stamp);
    }

    [Fact]
    public void Snapshot_metrics_reflect_accepted_and_processed_counts()
    {
        var inner = new FakeInnerRollingLogSink();
        using var sink = new AsyncRollingLogSink(inner, BasicOptions());

        sink.Info("a");
        sink.Info("b");
        sink.Info("c");
        sink.Flush(toDisk: false);

        var metrics = sink.SnapshotMetrics();
        metrics.Accepted.Should().Be(3);
        metrics.Processed.Should().Be(3);
        metrics.Dropped.Should().Be(0);
    }

    [Fact]
    public void Constructor_rejects_null_inner()
    {
        var act = () => new AsyncRollingLogSink(null!, BasicOptions());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Dispose_completes_within_a_bound_when_inner_sink_hangs()
    {
        using var gate = new ManualResetEventSlim(initialState: false);
        var inner = new FakeInnerRollingLogSink { GateOnLog = gate };
        var drainTimeout = TimeSpan.FromMilliseconds(150);
        var sink = new AsyncRollingLogSink(inner, BasicOptions() with { DrainOnDisposeTimeout = drainTimeout });

        // The single entry must pass through the gated inner write, so the worker ends up
        // wedged inside the inner sink and never observes the dispose cancel.
        sink.Info("entry-that-wedges-the-worker");
        WaitFor(() => sink.SnapshotMetrics().Accepted >= 1);
        Thread.Sleep(30);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        sink.Dispose();
        stopwatch.Stop();

        // Two bounded joins of drainTimeout each, plus slack — never the old unbounded hang.
        stopwatch.Elapsed.Should().BeLessThan(
            TimeSpan.FromSeconds(2),
            "disposal must not block forever on a wedged inner sink.");

        // Release the abandoned background worker so it can exit cleanly for the test run.
        gate.Set();
    }

    [Fact]
    public void Snapshot_metrics_after_dispose_is_safe()
    {
        var inner = new FakeInnerRollingLogSink();
        var sink = new AsyncRollingLogSink(inner, BasicOptions());
        sink.Info("payload");
        sink.Flush(toDisk: false);
        sink.Dispose();

        var act = () => sink.SnapshotMetrics();

        act.Should().NotThrow();
        sink.SnapshotMetrics().QueueDepth.Should().Be(0);
    }

    private static AsyncRollingLogSinkOptions BasicOptions() => AsyncRollingLogSinkOptions.Default with
    {
        QueueCapacity = AsyncRollingLogSinkOptions.MinimumQueueCapacity,
        BlockTimeout = TimeSpan.FromMilliseconds(100),
        FsyncInterval = null,
        FsyncOnCritical = false,
        DrainOnDisposeTimeout = TimeSpan.FromSeconds(1),
    };

    private static void WaitFor(Func<bool> predicate, TimeSpan? timeout = null)
    {
        var ceiling = timeout ?? TestTimeout;
        var deadline = DateTime.UtcNow + ceiling;
        while (!predicate() && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(5);
        }

        if (!predicate())
        {
            throw new TimeoutException("condition did not hold within " + ceiling);
        }
    }
}
