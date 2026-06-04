using System;
using System.IO;
using FluentAssertions;
using Opus.App.OpusAlpha.Cli;
using Opus.App.OpusAlpha.Run;
using Opus.App.OpusAlpha.Tests.Support;
using Opus.Engine.Host.Windows.Direct3D12;
using Opus.Foundation;
using Xunit;

namespace Opus.App.OpusAlpha.Tests.Run;

public sealed class OpusAlphaConsumerSmokeTests
{
    private const int SmokeFrames = 3;

    [SkippableFact]
    public void Smoke_cli_accepts_fake_consumer_and_steps_requested_frames()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Consumer smoke uses the Windows/D3D12 alpha host.");
        Skip.If(!CanOpenD3D12Host(), "D3D12 adapter / SDL video / DXC unavailable on this host.");

        var root = Path.Combine(Path.GetTempPath(), $"opus-consumer-smoke-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        using var sink = new StringWriter();
        var log = new ConsoleLog(LogLevel.Information, sink, sink, TimeProvider.System);
        var consumer = new FakeConsumerIntegration();
        var args = OpusAlphaCliParser.Parse(new[]
        {
            "smoke",
            "--frames",
            SmokeFrames.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--diagnostics-dir",
            root,
            "--report",
            Path.Combine(root, "reports"),
        });

        try
        {
            var exitCode = OpusAlphaSmokeRunner.Run(args, log, consumer.ToIntegration());

            exitCode.Should().Be(0, sink.ToString());
            consumer.StartedCount.Should().Be(1);
            consumer.FrameCount.Should().Be(SmokeFrames);
            consumer.StoppingCount.Should().Be(1);
            consumer.SceneRequests.Should().Be(SmokeFrames);
            consumer.AssetRequests.Should().Be(1);
            consumer.TelemetryRequests.Should().BeGreaterThan(0);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    private static bool CanOpenD3D12Host()
    {
        var options = D3D12OpusApplicationOptions.Default with
        {
            WindowTitle = "opus-consumer-smoke-probe",
            WindowWidth = 256,
            WindowHeight = 192,
        };
        using var instance = new D3D12OpusHostBuilder().WithOptions(options).TryBuild();
        return instance is not null;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
