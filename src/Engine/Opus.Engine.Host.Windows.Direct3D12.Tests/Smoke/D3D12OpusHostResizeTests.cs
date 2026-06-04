using System;
using System.IO;
using FluentAssertions;
using Opus.Foundation;
using Xunit;

namespace Opus.Engine.Host.Windows.Direct3D12.Tests.Smoke;

/// <summary>Live D3D12 coverage for the M11.13 resize path: a real adapter rebuilds the
/// swap chain + scene viewport + forward-renderer targets and regenerates the alpha-frame
/// plan, then keeps stepping. Drives <see cref="D3D12OpusApplication.Resize"/> directly —
/// SDL only delivers window-resize events on a real drag, which a headless run cannot
/// produce — so the test exercises the same code path the window bridge invokes.</summary>
public sealed class D3D12OpusHostResizeTests
{
    private const int FramesToStep = 3;
    private static readonly TimeSpan StepDelta = TimeSpan.FromMilliseconds(16.7);

    [SkippableFact]
    public void Resize_rebuilds_swap_chain_and_plan_then_keeps_stepping()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "D3D12 host smoke tests are Windows-only.");

        using var sink = new StringWriter();
        var log = new ConsoleLog(LogLevel.Information, sink, sink, TimeProvider.System);
        var options = D3D12OpusApplicationOptions.Default with
        {
            WindowTitle = "opus-host-resize",
            WindowWidth = 256,
            WindowHeight = 192,
        };

        D3D12OpusHostInstance? instance = null;
        try
        {
            instance = new D3D12OpusHostBuilder().WithLog(log).WithOptions(options).TryBuild();
            Skip.If(instance is null, "D3D12 adapter / SDL video / DXC unavailable on this host.");

            instance.Host.Start();
            StepFrames(instance);
            var initialViewport = instance.Application.Plan.SceneViewport;

            instance.Application.Resize(384, 288);
            StepFrames(instance);

            instance.Session.SwapChain.Width.Should().Be(384);
            instance.Session.SwapChain.Height.Should().Be(288);
            var resizedViewport = instance.Application.Plan.SceneViewport;
            resizedViewport.Width.Should().Be(360, because: "the viewport rect is back-buffer width minus the 24px UI margin.");
            resizedViewport.Height.Should().Be(238, because: "the viewport rect is back-buffer height minus the 50px UI margin.");
            resizedViewport.Should().NotBe(initialViewport, because: "the plan should regenerate for the new size.");

            // Below the alpha-frame minimum (a minimised window reports a tiny client area):
            // the host clamps it to a no-op and keeps the last valid surface.
            instance.Application.Resize(8, 8);
            StepFrames(instance);
            instance.Session.SwapChain.Width.Should().Be(384, because: "a sub-minimum resize is a no-op.");
            instance.Session.SwapChain.Height.Should().Be(288);

            instance.Host.Stop();
        }
        finally
        {
            instance?.Dispose();
        }
    }

    private static void StepFrames(D3D12OpusHostInstance instance)
    {
        for (var i = 0; i < FramesToStep; i++)
        {
            instance.Host.Step(StepDelta).Should().BeTrue($"frame {i} should not have stopped the host.");
        }
    }
}
