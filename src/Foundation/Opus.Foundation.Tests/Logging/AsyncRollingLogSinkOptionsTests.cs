using System;
using FluentAssertions;
using Opus.Foundation;
using Xunit;

namespace Opus.Foundation.Tests.Logging;

public sealed class AsyncRollingLogSinkOptionsTests
{
    [Fact]
    public void Default_options_pass_validation()
    {
        var act = () => AsyncRollingLogSinkOptions.Default.Validate();

        act.Should().NotThrow();
    }

    [Fact]
    public void Default_options_drop_oldest_and_fsync_on_critical()
    {
        var options = AsyncRollingLogSinkOptions.Default;

        options.BackpressurePolicy.Should().Be(RollingLogBackpressurePolicy.DropOldest);
        options.FsyncOnCritical.Should().BeTrue();
        options.FsyncInterval.Should().Be(AsyncRollingLogSinkOptions.DefaultFsyncInterval);
    }

    [Fact]
    public void Queue_capacity_below_minimum_rejected()
    {
        var options = AsyncRollingLogSinkOptions.Default with { QueueCapacity = 4 };

        var act = options.Validate;

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Unknown_backpressure_policy_rejected()
    {
        var options = AsyncRollingLogSinkOptions.Default with { BackpressurePolicy = (RollingLogBackpressurePolicy)99 };

        var act = options.Validate;

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Negative_block_timeout_rejected()
    {
        var options = AsyncRollingLogSinkOptions.Default with { BlockTimeout = TimeSpan.FromMilliseconds(-1) };

        var act = options.Validate;

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Zero_fsync_interval_rejected_when_set()
    {
        var options = AsyncRollingLogSinkOptions.Default with { FsyncInterval = TimeSpan.Zero };

        var act = options.Validate;

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Null_fsync_interval_accepted_as_disabled()
    {
        var options = AsyncRollingLogSinkOptions.Default with { FsyncInterval = null };

        var act = options.Validate;

        act.Should().NotThrow();
    }

    [Fact]
    public void Negative_drain_timeout_rejected()
    {
        var options = AsyncRollingLogSinkOptions.Default with { DrainOnDisposeTimeout = TimeSpan.FromMilliseconds(-1) };

        var act = options.Validate;

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Tail_below_one_rejected()
    {
        var options = AsyncRollingLogSinkOptions.Default with { MaxTailEntries = 0 };

        var act = options.Validate;

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Blank_worker_thread_name_rejected()
    {
        var options = AsyncRollingLogSinkOptions.Default with { WorkerThreadName = "   " };

        var act = options.Validate;

        act.Should().Throw<ArgumentException>();
    }
}
