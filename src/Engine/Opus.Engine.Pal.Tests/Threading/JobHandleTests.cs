using System.Threading.Tasks;
using FluentAssertions;
using Opus.Engine.Pal.Threading;
using Xunit;

namespace Opus.Engine.Pal.Tests.Threading;

public sealed class JobHandleTests
{
    [Fact]
    public void Default_handle_is_completed()
    {
        JobHandle.Completed.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task Wraps_underlying_task()
    {
        var tcs = new TaskCompletionSource();
        var handle = JobHandle.FromTask(tcs.Task);

        handle.IsCompleted.Should().BeFalse();
        tcs.SetResult();
        await handle.AsTask();
        handle.IsCompleted.Should().BeTrue();
    }
}
