using FluentAssertions;
using Opus.Engine.Pal.Application;
using Opus.Engine.Pal.Windows.Application;
using Xunit;

namespace Opus.Engine.Pal.Tests.Application;

public sealed class WindowsLifecycleServiceTests
{
    [Fact]
    public void Shutdown_is_emitted_once_and_is_terminal()
    {
        using var lifecycle = new WindowsLifecycleService();
        var shutdownCount = 0;
        lifecycle.ShuttingDown += () => shutdownCount++;

        lifecycle.EnterForeground();
        lifecycle.BeginShutdown();
        lifecycle.EnterBackground();
        lifecycle.BeginShutdown();

        lifecycle.State.Should().Be(LifecycleState.ShuttingDown);
        shutdownCount.Should().Be(1);
    }

    [Fact]
    public void Dispose_transitions_to_shutdown_once()
    {
        var lifecycle = new WindowsLifecycleService();
        var shutdownCount = 0;
        lifecycle.ShuttingDown += () => shutdownCount++;

        lifecycle.Dispose();
        lifecycle.Dispose();

        lifecycle.State.Should().Be(LifecycleState.ShuttingDown);
        shutdownCount.Should().Be(1);
    }
}
