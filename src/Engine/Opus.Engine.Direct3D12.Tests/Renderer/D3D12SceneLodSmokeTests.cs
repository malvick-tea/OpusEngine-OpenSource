using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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

/// <summary>Live D3D12 smoke for coarse LOD: renders near + far copies of one fine disc through
/// the forward pass once with LOD opted out and once opted in, and proves that opting in reselects
/// the distant copies to the coarse mesh — fewer indices submitted
/// (<see cref="D3D12ForwardSceneRenderer.LastDrawnIndexCount"/> drops) while the same instances are
/// drawn (primitive-instance count unchanged) and nothing is culled — then composites a non-black
/// frame. Isolates LOD from culling by keeping every instance inside the frustum and running the
/// identical bounds (so culling drops nothing) in both passes. Skips when no D3D12 adapter exists.</summary>
public sealed unsafe class D3D12SceneLodSmokeTests
{
    private static readonly float[] NearDistances = { 30f, 40f, 50f };
    private static readonly float[] FarDistances = { 90f, 110f, 130f };

    [SkippableFact]
    public void Coarse_lod_reselects_distant_instances_to_a_cheaper_mesh_and_keeps_a_visible_frame()
    {
        using var host = D3D12SmokeHost.OpenWindow("opus-d3d12-lod-smoke", width: 640, height: 360);
        var plan = D3D12AlphaFramePlan.Create(host.Session.SwapChain.Width, host.Session.SwapChain.Height);
        using var scene = LodSmokeScene.Create(host.Session.Device, "lod-smoke");
        using var sceneViewport = new D3D12SceneViewport(
            host.Session.Device,
            host.Session.SwapChain,
            plan.SceneViewport.Width,
            plan.SceneViewport.Height,
            "lod-smoke.viewport");
        using var sceneRenderer = new D3D12ForwardSceneRenderer(
            host.Session.Device,
            host.Session.Compiler,
            sceneViewport.Target.Format,
            plan.SceneViewport.Width,
            plan.SceneViewport.Height,
            "lod-smoke.forward");
        using var frameLoop = new D3D12UiFrameLoop(host.Session);
        using var atlas = host.BuildFontAtlas(plan.UiText);
        using var surface = D3D12DrawSurface.Create(
            host.Session.Device,
            atlas,
            host.Session.Compiler,
            host.Session.SwapChain.Format,
            frameSlots: D3D12SwapChain.BufferCount,
            maxQuadsPerFrame: 512);

        // Place every instance on the camera's view ray so all stay inside the frustum (culling
        // drops nothing): the near group inside the LOD threshold, the far group beyond it. Both
        // start on the fine mesh; only LOD distance reselects the far ones to the coarse mesh.
        var camera = plan.Cameras.Main;
        var draws = NearDistances.Concat(FarDistances)
            .Select(distance => new SceneNodeDraw(
                scene.FineMeshIndex,
                Matrix4x4.CreateTranslation(camera.PositionWorld + camera.ForwardWorld * distance)))
            .ToList();
        var farCount = FarDistances.Length;

        D3D12DebugAssertions.Clear(host.Session.Device);

        // Opted-out: same bounds (so culling behaves identically) but no LOD chains — every
        // instance renders at full detail, all on the fine mesh, in a single instanced batch.
        sceneRenderer.Render(
            sceneViewport.Renderer,
            scene.GpuScene,
            draws,
            scene.Atlas,
            plan.Cameras,
            plan.Lighting,
            plan.PostFx,
            sceneViewport.CreateRenderTargetDescriptor(),
            scene.MeshLocalBounds);
        sceneRenderer.LastCulledNodeCount.Should().Be(0, "every instance sits on the view ray, inside the frustum");
        sceneRenderer.LastLodDemotedNodeCount.Should().Be(0, "no LOD chains were supplied");
        var fullIndexCount = sceneRenderer.LastDrawnIndexCount;
        var fullPrimitiveInstances = sceneRenderer.LastDrawnPrimitiveCount;
        fullIndexCount.Should().Be(draws.Count * scene.FineIndexCount, "all instances submit the fine index count");
        host.Session.Device.WaitForIdle();

        // Opted-in: the far group reselects to the coarse mesh, submitting fewer indices for the
        // same instances — the LOD win, with no change to how many instances are drawn.
        sceneRenderer.Render(
            sceneViewport.Renderer,
            scene.GpuScene,
            draws,
            scene.Atlas,
            plan.Cameras,
            plan.Lighting,
            plan.PostFx,
            sceneViewport.CreateRenderTargetDescriptor(),
            scene.MeshLocalBounds,
            scene.MeshLods);
        sceneRenderer.LastCulledNodeCount.Should().Be(0, "LOD must not cull — it reselects, never drops");
        sceneRenderer.LastLodDemotedNodeCount.Should().Be(farCount, "every far instance drops below its finest level");
        sceneRenderer.LastDrawnPrimitiveCount.Should()
            .Be(fullPrimitiveInstances, "LOD draws the same instances — only their mesh detail changes");
        var expectedLodIndexCount = (NearDistances.Length * scene.FineIndexCount) + (farCount * scene.CoarseIndexCount);
        sceneRenderer.LastDrawnIndexCount.Should()
            .Be(expectedLodIndexCount, "near instances keep the fine index count, far instances submit the coarse one");
        sceneRenderer.LastDrawnIndexCount.Should()
            .BeLessThan(fullIndexCount, "reselecting distant instances to the coarse mesh lowers the triangle budget");

        // Composite the LOD viewport into the back buffer and prove it is not a black frame.
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
            "lod-smoke.screenshot");
        readback.RecordCopyFrom(
            frame.CommandList,
            host.Session.SwapChain.CurrentBackBuffer,
            ResourceStates.RenderTarget,
            ResourceStates.RenderTarget);
        frameLoop.EndFrame();
        host.Session.Device.WaitForIdle();

        var screenshot = readback.ReadRgba8();
        HasVisibleRgb(screenshot.Rgba8).Should().BeTrue("the near fine disc should render visible pixels");

        D3D12DebugAssertions.ShouldHaveNoErrors(host.Session.Device);
    }

    private static bool HasVisibleRgb(byte[] rgba) =>
        rgba.Chunk(4).Any(pixel => pixel[0] > 18 || pixel[1] > 18 || pixel[2] > 18);
}
