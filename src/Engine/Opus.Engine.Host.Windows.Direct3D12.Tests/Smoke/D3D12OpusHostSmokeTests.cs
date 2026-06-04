using System;
using System.Diagnostics;
using System.IO;
using FluentAssertions;
using Opus.Foundation;
using Xunit;

namespace Opus.Engine.Host.Windows.Direct3D12.Tests.Smoke;

public sealed class D3D12OpusHostSmokeTests
{
    private const int FramesToStep = 8;
    private static readonly TimeSpan StepDelta = TimeSpan.FromMilliseconds(16.7);

    [SkippableFact]
    public void Host_steps_eight_frames_over_d3d12_then_stops_cleanly()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "D3D12 host smoke tests are Windows-only.");

        using var sink = new StringWriter();
        var log = new ConsoleLog(LogLevel.Information, sink, sink, TimeProvider.System);
        var options = D3D12OpusApplicationOptions.Default with
        {
            WindowTitle = "opus-host-smoke",
            WindowWidth = 256,
            WindowHeight = 192,
        };

        D3D12OpusHostInstance? instance = null;
        try
        {
            instance = new D3D12OpusHostBuilder().WithLog(log).WithOptions(options).TryBuild();
            Skip.If(instance is null, "D3D12 adapter / SDL video / DXC unavailable on this host.");

            instance.Host.Start();
            for (var i = 0; i < FramesToStep; i++)
            {
                var alive = instance.Host.Step(StepDelta);
                alive.Should().BeTrue($"frame {i} should not have stopped the host prematurely.");
            }

            instance.Application.Metrics.TotalFramesObserved.Should().Be(FramesToStep);
            var snapshot = instance.Application.Metrics.Snapshot();
            snapshot.SampleCount.Should().Be(FramesToStep);
            snapshot.Max.Should().BeGreaterThan(TimeSpan.Zero);

            instance.Host.Stop();
            sink.ToString().Should().Contain(EngineIdentity.Current.DisplayName, because: "OnStarted should log the build banner.");
            sink.ToString().Should().Contain("Stopping after", because: "OnStopping should log the rolling metrics summary.");
        }
        finally
        {
            instance?.Dispose();
        }
    }

    [SkippableFact]
    public void Screenshot_request_is_consumed_within_one_frame()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "D3D12 host smoke tests are Windows-only.");

        using var sink = new StringWriter();
        var log = new ConsoleLog(LogLevel.Information, sink, sink, TimeProvider.System);
        var options = D3D12OpusApplicationOptions.Default with
        {
            WindowTitle = "opus-host-screenshot",
            WindowWidth = 256,
            WindowHeight = 192,
        };

        var screenshotPath = Path.Combine(Path.GetTempPath(), $"opus-host-smoke-{Guid.NewGuid():N}.png");
        D3D12OpusHostInstance? instance = null;
        try
        {
            instance = new D3D12OpusHostBuilder().WithLog(log).WithOptions(options).TryBuild();
            Skip.If(instance is null, "D3D12 adapter / SDL video / DXC unavailable on this host.");

            instance.Host.Start();
            instance.Host.Step(StepDelta);
            instance.Application.RequestScreenshot(screenshotPath);
            instance.Host.Step(StepDelta);
            instance.Host.Stop();

            File.Exists(screenshotPath).Should().BeTrue("the host should have written a PNG capture.");
            instance.Application.LastScreenshotPath.Should().Be(screenshotPath);
            var bytes = File.ReadAllBytes(screenshotPath);
            bytes.Length.Should().BeGreaterThan(8);
            bytes[0].Should().Be(0x89, because: "PNG signature byte 0.");
            bytes[1].Should().Be((byte)'P');
            bytes[2].Should().Be((byte)'N');
            bytes[3].Should().Be((byte)'G');
        }
        finally
        {
            instance?.Dispose();
            if (File.Exists(screenshotPath))
            {
                try
                {
                    File.Delete(screenshotPath);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
