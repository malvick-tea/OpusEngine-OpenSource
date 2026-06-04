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

/// <summary>Live D3D12 smoke for large-map frustum culling: renders the wide-map alpha
/// scene with a block of guaranteed off-frustum tank instances appended, once with culling
/// opted out and once opted in, and proves that opting in drops the off-screen instances
/// (<see cref="D3D12ForwardSceneRenderer.LastCulledNodeCount"/> &gt; 0, fewer primitives
/// submitted) while the visible subset still produces a non-black frame. Skips when no
/// D3D12 adapter is available.</summary>
public sealed unsafe class D3D12LargeMapCullingSmokeTests
{
    [SkippableFact]
    public void Frustum_culling_drops_offscreen_instances_and_keeps_a_visible_frame()
    {
        using var host = D3D12SmokeHost.OpenWindow("opus-d3d12-cull-smoke", width: 640, height: 360);
        var plan = D3D12AlphaFramePlan.Create(host.Session.SwapChain.Width, host.Session.SwapChain.Height);
        using var asset = AlphaSmokeGltfAsset.WriteTempGlb();
        using var assets = GarageSceneAssets.Load(host.Session.Device, asset.Path, "cull-smoke");
        using var sceneViewport = new D3D12SceneViewport(
            host.Session.Device,
            host.Session.SwapChain,
            plan.SceneViewport.Width,
            plan.SceneViewport.Height,
            "cull-smoke.viewport");
        using var sceneRenderer = new D3D12ForwardSceneRenderer(
            host.Session.Device,
            host.Session.Compiler,
            sceneViewport.Target.Format,
            plan.SceneViewport.Width,
            plan.SceneViewport.Height,
            "cull-smoke.forward");
        using var frameLoop = new D3D12UiFrameLoop(host.Session);
        using var atlas = host.BuildFontAtlas(plan.UiText);
        using var surface = D3D12DrawSurface.Create(
            host.Session.Device,
            atlas,
            host.Session.Compiler,
            host.Session.SwapChain.Format,
            frameSlots: D3D12SwapChain.BufferCount,
            maxQuadsPerFrame: 512);

        var tankInputs = new GarageSceneDrawBuilder.TankInstancesInput(
            Matrix4x4.CreateScale(6f),
            new Vector4(0.92f, 1.0f, 0.74f, 1f),
            plan.OpponentTanks,
            plan.OpponentTints);
        var transientInputs = new GarageSceneDrawBuilder.TransientsInput(
            plan.ProjectileTrails,
            assets.ProjectileMeshIndex,
            ShellTemplate: null,
            ShellHeads: null,
            plan.Casings,
            assets.CasingMeshIndex);
        var onMapDraws = GarageSceneDrawBuilder.Build(
            assets.TankTemplate, assets.StaticDraws, in tankInputs, in transientInputs);

        // Append a block of tank instances shoved 100k units off to the side — far outside
        // the wide-map camera frustum on every axis, so culling must drop exactly these.
        var draws = new List<SceneNodeDraw>(onMapDraws);
        foreach (var node in assets.TankTemplate)
        {
            draws.Add(new SceneNodeDraw(
                node.MeshIndex,
                node.World * Matrix4x4.CreateTranslation(100_000f, 0f, 0f)));
        }

        D3D12DebugAssertions.Clear(host.Session.Device);

        // Opted-out render: every node is submitted, nothing culled.
        sceneRenderer.Render(
            sceneViewport.Renderer,
            assets.GpuScene,
            draws,
            assets.Atlas,
            plan.Cameras,
            plan.Lighting,
            plan.PostFx,
            sceneViewport.CreateRenderTargetDescriptor());
        sceneRenderer.LastCulledNodeCount.Should().Be(0, "culling is opt-in — no bounds means no culling");
        var unculledPrimitives = sceneRenderer.LastDrawnPrimitiveCount;
        unculledPrimitives.Should().BeGreaterThan(0);
        host.Session.Device.WaitForIdle();

        // Opted-in render: the off-frustum block is dropped before the draw loop.
        sceneRenderer.Render(
            sceneViewport.Renderer,
            assets.GpuScene,
            draws,
            assets.Atlas,
            plan.Cameras,
            plan.Lighting,
            plan.PostFx,
            sceneViewport.CreateRenderTargetDescriptor(),
            assets.MeshLocalBounds);
        sceneRenderer.LastCulledNodeCount.Should()
            .BeGreaterThanOrEqualTo(assets.TankTemplate.Count, "every appended off-map tank node must be culled");
        sceneRenderer.LastDrawnPrimitiveCount.Should()
            .BeLessThan(unculledPrimitives, "culling the off-map block must submit fewer primitives");
        sceneRenderer.LastDrawnPrimitiveCount.Should()
            .BeGreaterThan(0, "the on-map instances must still render");

        // Composite the culled viewport into the back buffer and prove it is not a black frame.
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
            "cull-smoke.screenshot");
        readback.RecordCopyFrom(
            frame.CommandList,
            host.Session.SwapChain.CurrentBackBuffer,
            ResourceStates.RenderTarget,
            ResourceStates.RenderTarget);
        frameLoop.EndFrame();
        host.Session.Device.WaitForIdle();

        var screenshot = readback.ReadRgba8();
        HasVisibleRgb(screenshot.Rgba8).Should().BeTrue("the culled scene should still draw its on-screen instances");

        D3D12DebugAssertions.ShouldHaveNoErrors(host.Session.Device);
    }

    private static bool HasVisibleRgb(byte[] rgba) =>
        rgba.Chunk(4).Any(pixel => pixel[0] > 18 || pixel[1] > 18 || pixel[2] > 18);
}
