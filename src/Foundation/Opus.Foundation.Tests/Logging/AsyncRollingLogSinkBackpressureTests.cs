using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Opus.Foundation;
using Xunit;

namespace Opus.Foundation.Tests.Logging;

public sealed class AsyncRollingLogSinkBackpressureTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(2);

    [Fact]
    public void Drop_oldest_evicts_earliest_queued_entries_when_full()
    {
        using var gate = new ManualResetEventSlim(initialState: false);
        var inner = new FakeInnerRollingLogSink { GateOnLog = gate };
        using var sink = new AsyncRollingLogSink(
            inner,
            DropOldestOptions(capacity: AsyncRollingLogSinkOptions.MinimumQueueCapacity));

        var overload = AsyncRollingLogSinkOptions.MinimumQueueCapacity * 4;
        for (var i = 0; i < overload; i++)
        {
            sink.Info("entry-" + i);
        }

        sink.SnapshotMetrics().Dropped.Should().BeGreaterThan(0);
        gate.Set();
        WaitFor(() => sink.SnapshotMetrics().Processed > 0);
    }

    [Fact]
    public void Drop_newest_discards_new_entries_when_full()
    {
        using var gate = new ManualResetEventSlim(initialState: false);
        var inner = new FakeInnerRollingLogSink { GateOnLog = gate };
        var capacity = AsyncRollingLogSinkOptions.MinimumQueueCapacity;
        using var sink = new AsyncRollingLogSink(
            inner,
            DropOldestOptions(capacity) with { BackpressurePolicy = RollingLogBackpressurePolicy.DropNewest });

        for (var i = 0; i < capacity * 4; i++)
        {
            sink.Info("entry-" + i);
        }

        sink.SnapshotMetrics().Dropped.Should().BeGreaterThan(0);
        gate.Set();
    }

    [Fact]
    public void Block_policy_drops_after_timeout()
    {
        using var gate = new ManualResetEventSlim(initialState: false);
        var inner = new FakeInnerRollingLogSink { GateOnLog = gate };
        var capacity = AsyncRollingLogSinkOptions.MinimumQueueCapacity;
        using var sink = new AsyncRollingLogSink(
            inner,
            DropOldestOptions(capacity) with
            {
                BackpressurePolicy = RollingLogBackpressurePolicy.Block,
                BlockTimeout = TimeSpan.FromMilliseconds(40),
            });

        for (var i = 0; i < capacity * 3; i++)
        {
            sink.Info("entry-" + i);
        }

        sink.SnapshotMetrics().Dropped.Should().BeGreaterThan(0);
        gate.Set();
    }

    [Fact]
    public async Task Block_policy_admits_producer_once_worker_drains_a_slot()
    {
        using var gate = new ManualResetEventSlim(initialState: false);
        var inner = new FakeInnerRollingLogSink { GateOnLog = gate };
        var capacity = AsyncRollingLogSinkOptions.MinimumQueueCapacity;
        using var sink = new AsyncRollingLogSink(
            inner,
            DropOldestOptions(capacity) with
            {
                BackpressurePolicy = RollingLogBackpressurePolicy.Block,
                BlockTimeout = TimeSpan.FromSeconds(5),
            });

        // The worker grabs its first entry and parks on the gate, so it can never drain the
        // queue while the gate is closed. A background producer therefore saturates the
        // bounded queue and blocks. Accept count plateaus deterministically at
        // capacity + 1 (capacity in the bounded queue plus the one in the worker's hand),
        // independent of producer/worker interleaving.
        var lastMessage = "entry-" + ((capacity * 2) - 1);
        var producer = Task.Run(() =>
        {
            for (var i = 0; i < capacity * 2; i++)
            {
                sink.Info("entry-" + i);
            }
        });

        try
        {
            WaitFor(() => sink.SnapshotMetrics().Accepted >= capacity + 1);
            producer.IsCompleted.Should().BeFalse(
                "the gated worker frees no slots, so the producer must be blocked at the saturation plateau.");
        }
        finally
        {
            // Always release the worker so the producer can never stay blocked across the
            // sink disposal at end of scope. A producer left blocked on a torn-down queue is
            // the orphaned-thread shape that crashes the test host.
            gate.Set();
        }

        await producer.WaitAsync(TestTimeout);
        sink.Flush(toDisk: false);

        inner.WrittenEntries.Select(e => e.Message).Should().Contain(lastMessage);
    }

    private static AsyncRollingLogSinkOptions DropOldestOptions(int capacity)
        => AsyncRollingLogSinkOptions.Default with
        {
            QueueCapacity = capacity,
            BackpressurePolicy = RollingLogBackpressurePolicy.DropOldest,
            FsyncInterval = null,
            FsyncOnCritical = false,
            DrainOnDisposeTimeout = TimeSpan.FromSeconds(1),
        };

    private static void WaitFor(Func<bool> predicate)
    {
        var deadline = DateTime.UtcNow + TestTimeout;
        while (!predicate() && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(5);
        }

        if (!predicate())
        {
            throw new TimeoutException("condition did not hold within " + TestTimeout);
        }
    }
}
