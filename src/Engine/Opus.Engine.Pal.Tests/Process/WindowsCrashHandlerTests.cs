using System;
using System.IO;
using FluentAssertions;
using Opus.Engine.Pal.Windows.Process;
using Xunit;

namespace Opus.Engine.Pal.Tests.Process;

public sealed class WindowsCrashHandlerTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "opus-crash-handler-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Install_and_dispose_manage_global_subscription_idempotently()
    {
        var handler = new WindowsCrashHandler(_directory);

        handler.Install();
        handler.Install();
        handler.IsInstalled.Should().BeTrue();

        handler.Dispose();
        handler.Dispose();
        handler.IsInstalled.Should().BeFalse();
    }

    [Fact]
    public void Disposed_handler_rejects_new_work()
    {
        var handler = new WindowsCrashHandler(_directory);
        handler.Dispose();

        handler.Invoking(value => value.Install())
            .Should().Throw<ObjectDisposedException>();
        handler.Invoking(value => value.AddBreadcrumb("network", "closed"))
            .Should().Throw<ObjectDisposedException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
