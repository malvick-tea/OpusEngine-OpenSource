using System;
using System.IO;
using FluentAssertions;
using Opus.App.OpusAlpha.Run;
using Opus.Engine.Host.Windows.Direct3D12;
using Opus.Foundation;
using Xunit;

namespace Opus.App.OpusAlpha.Tests.Run;

public sealed class OpusAlphaLoggingTests
{
    [Fact]
    public void TryCreateRollingLog_returns_plain_file_sink_by_default()
    {
        using var temp = TempDirectory.Create();
        var options = OptionsForDiagnosticsRoot(temp.Path);

        using var sink = OpusAlphaLogging.TryCreateRollingLog(options, NullLog.Instance);

        sink.Should().BeOfType<RollingFileLogSink>(
            "the host default stays synchronous until tester throughput data justifies async writes.");
    }

    [Fact]
    public void TryCreateRollingLog_wraps_in_async_sink_when_requested()
    {
        using var temp = TempDirectory.Create();
        var options = OptionsForDiagnosticsRoot(temp.Path);

        using var sink = OpusAlphaLogging.TryCreateRollingLog(options, NullLog.Instance, useAsyncWrites: true);

        sink.Should().BeOfType<AsyncRollingLogSink>();
    }

    [Fact]
    public void Async_wrapped_sink_writes_through_to_the_log_file_on_dispose()
    {
        using var temp = TempDirectory.Create();
        var options = OptionsForDiagnosticsRoot(temp.Path);
        var sink = OpusAlphaLogging.TryCreateRollingLog(options, NullLog.Instance, useAsyncWrites: true);
        sink.Should().NotBeNull();

        var logFilePath = sink!.CurrentLogFilePath;
        sink.Log(LogLevel.Warning, "async-write-marker");
        sink.Dispose();

        File.Exists(logFilePath).Should().BeTrue();
        File.ReadAllText(logFilePath).Should().Contain(
            "async-write-marker",
            "disposing the async wrapper drains the queue into the inner file sink.");
    }

    private static D3D12OpusApplicationOptions OptionsForDiagnosticsRoot(string root) =>
        D3D12OpusApplicationOptions.Default with { DiagnosticsDirectory = root };

    private sealed class TempDirectory : IDisposable
    {
        private TempDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TempDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "opus-async-logging-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TempDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
