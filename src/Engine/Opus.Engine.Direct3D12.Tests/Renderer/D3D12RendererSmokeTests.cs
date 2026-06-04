using FluentAssertions;
using Opus.Engine.Direct3D12.Tests.Fixtures;
using Opus.Engine.Renderer.Direct3D12;
using Xunit;

namespace Opus.Engine.Direct3D12.Tests.Renderer;

public sealed class D3D12RendererSmokeTests
{
    [SkippableFact]
    public void Renderer_opens_and_presents_empty_frame()
    {
        using var host = D3D12SmokeHost.OpenWindow("opus-d3d12-renderer-smoke");
        using var renderer = new D3D12Renderer(host.Session.Device, host.Session.SwapChain, "smoke.renderer");

        var context = renderer.BeginFrame(
            D3D12RendererSmokeDefaults.Cameras,
            D3D12RendererSmokeDefaults.Lighting,
            D3D12RendererSmokeDefaults.PostFx);
        renderer.EndFrame(context);

        renderer.FrameIndex.Should().Be(1);
        renderer.FrameGraphConcrete.IsCompiled.Should().BeTrue();
    }
}
