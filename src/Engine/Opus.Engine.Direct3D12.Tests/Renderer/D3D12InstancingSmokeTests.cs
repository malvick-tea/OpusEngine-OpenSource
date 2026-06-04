using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Opus.Engine.Direct3D12.Tests.Fixtures;
using Opus.Engine.Pal.Windows.Direct3D12;
using Opus.Engine.Renderer.Direct3D12;
using Opus.Engine.Renderer.Direct3D12.Alpha;
using Opus.Engine.Renderer.Direct3D12.Assets;
using Opus.Engine.Renderer.Direct3D12.Scene;
using Opus.Engine.Rhi.Direct3D12;
using Opus.Engine.Ui;
using Opus.Engine.Ui.Direct3D12;
using Silk.NET.Direct3D12;
using Xunit;

namespace Opus.Engine.Direct3D12.Tests.Renderer;

/// <summary>Live D3D12 smoke for GPU instancing: renders many copies of one mesh and proves the
/// forward pass collapses them into a single set of primitive draw calls (one
/// <c>DrawIndexedInstanced</c> per primitive, fanned across every instance) while still drawing a
/// non-black frame. Skips when no D3D12 adapter is available.</summary>
public sealed unsafe class D3D12InstancingSmokeTests
{
    [SkippableFact]
    public void Instanced_forward_pass_collapses_repeated_meshes_into_one_draw_call_each()
    {
        using var host = D3D12SmokeHost.OpenWindow("opus-d3d12-instancing-smoke", width: 640, height: 360);
        var plan = D3D12AlphaFramePlan.Create(host.Session.SwapChain.Width, host.Session.SwapChain.Height);
        using var asset = AlphaSmokeGltfAsset.WriteTempGlb();
        using var assets = GarageSceneAssets.Load(host.Session.Device, asset.Path, "instancing-smoke");
        using var sceneViewport = new D3D12SceneViewport(
            host.Session.Device,
            host.Session.SwapChain,
            plan.SceneViewport.Width,
            plan.SceneViewport.Height,
            "instancing-smoke.viewport");
        using var sceneRenderer = new D3D12ForwardSceneRenderer(
            host.Session.Device,
            host.Session.Compiler,
            sceneViewport.Target.Format,
            plan.SceneViewport.Width,
            plan.SceneViewport.Height,
            "instancing-smoke.forward");
        using var frameLoop = new D3D12UiFrameLoop(host.Session);
        using var atlas = host.BuildFontAtlas(plan.UiText);
        using var surface = D3D12DrawSurface.Create(
            host.Session.Device,
            atlas,
            host.Session.Compiler,
            host.Session.SwapChain.Format,
            frameSlots: D3D12SwapChain.BufferCount,
            maxQuadsPerFrame: 512);

        // One mesh, N in-frame instances: every opponent-grid position renders the same tank
        // mesh. With instancing this is exactly one DrawIndexedInstanced per mesh primitive
        // fanned across all N, so the primitive-instance total is N times the draw-call count.
        var meshIndex = assets.TankTemplate[0].MeshIndex;
        var instanceWorlds = plan.OpponentTanks;
        instanceWorlds.Count.Should().BeGreaterThan(1, "the instancing win is only meaningful with repeated copies");
        var draws = instanceWorlds.Select(world => new SceneNodeDraw(meshIndex, world)).ToList();

        D3D12DebugAssertions.Clear(host.Session.Device);
        sceneRenderer.Render(
            sceneViewport.Renderer,
            assets.GpuScene,
            draws,
            assets.Atlas,
            plan.Cameras,
            plan.Lighting,
            plan.PostFx,
            sceneViewport.CreateRenderTargetDescriptor());

        var drawCalls = sceneRenderer.LastDrawCallCount;
        var primitiveInstances = sceneRenderer.LastDrawnPrimitiveCount;
        drawCalls.Should().BeGreaterThan(0, "the single mesh has at least one primitive");
        drawCalls.Should().BeLessThan(draws.Count, "N copies of one mesh must not cost N draw calls");
        primitiveInstances.Should().Be(
            drawCalls * draws.Count,
            "each of the {0} primitive draw calls fans across all {1} instances", drawCalls, draws.Count);

        // Composite the instanced viewport and prove it is not a black frame.
        host.Session.Window.PollEvents();
        var frame = frameLoop.BeginFrame();
        surface.BeginFrame(
            frame.CommandList,
            frame.RenderTargetView,
            frame.BackBufferSlot,
            frame.ViewportWidth,
            frame.ViewportHeight);
        surface.Clear(Color.FromRgb(4, 6, 10));
        surface.DrawTexturedRect(
            sceneViewport.Target.SrvTable,
            sceneViewport.Target.SrvHeap,
            plan.SceneViewport.X,
            plan.SceneViewport.Y,
            plan.SceneViewport.Width,
            plan.SceneViewport.Height);
        surface.EndFrame();

        using var readback = D3D12TextureReadback.CreateForCurrentBackBuffer(
            host.Session.Device,
            host.Session.SwapChain,
            "instancing-smoke.screenshot");
        readback.RecordCopyFrom(
            frame.CommandList,
            host.Session.SwapChain.CurrentBackBuffer,
            ResourceStates.RenderTarget,
            ResourceStates.RenderTarget);
        frameLoop.EndFrame();
        host.Session.Device.WaitForIdle();

        var screenshot = readback.ReadRgba8();
        HasVisibleRgb(screenshot.Rgba8).Should().BeTrue("the instanced mesh copies should render");

        D3D12DebugAssertions.ShouldHaveNoErrors(host.Session.Device);
    }

    private static bool HasVisibleRgb(byte[] rgba) =>
        rgba.Chunk(4).Any(pixel => pixel[0] > 18 || pixel[1] > 18 || pixel[2] > 18);
}
