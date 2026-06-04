using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using FluentAssertions;
using Opus.Engine.Direct3D12.Tests.Fixtures;
using Opus.Engine.Pal.Windows.Direct3D12;
using Opus.Engine.Renderer.Direct3D12;
using Opus.Engine.Renderer.Direct3D12.Alpha;
using Opus.Engine.Renderer.Direct3D12.Scene;
using Opus.Engine.Rhi.Direct3D12;
using Opus.Engine.Ui;
using Opus.Engine.Ui.Direct3D12;
using Silk.NET.Direct3D12;
using Xunit;

namespace Opus.Engine.Direct3D12.Tests.Renderer;

public sealed unsafe class D3D12AlphaPathSmokeTests
{
    [SkippableFact]
    public void Alpha_path_renders_scene_viewport_ui_text_large_map_and_screenshot()
    {
        using var host = D3D12SmokeHost.OpenWindow("opus-d3d12-alpha-path-smoke", width: 640, height: 360);
        var plan = D3D12AlphaFramePlan.Create(host.Session.SwapChain.Width, host.Session.SwapChain.Height);
        using var asset = AlphaSmokeGltfAsset.WriteTempGlb();
        using var assets = GarageSceneAssets.Load(host.Session.Device, asset.Path, "alpha-smoke");
        using var sceneViewport = new D3D12SceneViewport(
            host.Session.Device,
            host.Session.SwapChain,
            plan.SceneViewport.Width,
            plan.SceneViewport.Height,
            "alpha-smoke.viewport");
        using var sceneRenderer = new D3D12ForwardSceneRenderer(
            host.Session.Device,
            host.Session.Compiler,
            sceneViewport.Target.Format,
            plan.SceneViewport.Width,
            plan.SceneViewport.Height,
            "alpha-smoke.forward");
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
        var draws = GarageSceneDrawBuilder.Build(assets.TankTemplate, assets.StaticDraws, in tankInputs, in transientInputs);
        draws.Count.Should().BeGreaterThan(plan.MapInstanceCount / 2);

        D3D12DebugAssertions.Clear(host.Session.Device);
        var stopwatch = Stopwatch.StartNew();
        sceneRenderer.Render(
            sceneViewport.Renderer,
            assets.GpuScene,
            draws,
            assets.Atlas,
            plan.Cameras,
            plan.Lighting,
            plan.PostFx,
            sceneViewport.CreateRenderTargetDescriptor());

        host.Session.Window.PollEvents();
        var frame = frameLoop.BeginFrame();
        surface.BeginFrame(
            frame.CommandList,
            frame.RenderTargetView,
            frame.BackBufferSlot,
            frame.ViewportWidth,
            frame.ViewportHeight);
        surface.Clear(Color.FromRgb(4, 6, 10));
        surface.FillRect(0, 0, frame.ViewportWidth, 30, Color.FromRgb(18, 24, 34));
        surface.DrawText(plan.UiText[0], 12, 7, 18, Color.White);
        surface.DrawTexturedRect(
            sceneViewport.Target.SrvTable,
            sceneViewport.Target.SrvHeap,
            plan.SceneViewport.X,
            plan.SceneViewport.Y,
            plan.SceneViewport.Width,
            plan.SceneViewport.Height);
        surface.StrokeRect(
            plan.SceneViewport.X,
            plan.SceneViewport.Y,
            plan.SceneViewport.Width,
            plan.SceneViewport.Height,
            2,
            Color.FromRgb(88, 142, 208));
        surface.DrawText($"draws {draws.Count} / map {plan.MapInstanceCount}", 14, frame.ViewportHeight - 24, 16, Color.White);
        surface.EndFrame();

        using var readback = D3D12TextureReadback.CreateForCurrentBackBuffer(
            host.Session.Device,
            host.Session.SwapChain,
            "alpha-smoke.screenshot");
        readback.RecordCopyFrom(
            frame.CommandList,
            host.Session.SwapChain.CurrentBackBuffer,
            ResourceStates.RenderTarget,
            ResourceStates.RenderTarget);
        frameLoop.EndFrame();
        stopwatch.Stop();
        host.Session.Device.WaitForIdle();

        var screenshot = readback.ReadRgba8();
        screenshot.Width.Should().Be(host.Session.SwapChain.Width);
        screenshot.Height.Should().Be(host.Session.SwapChain.Height);
        HasVisibleRgb(screenshot.Rgba8).Should().BeTrue("the alpha path should not produce a black frame");

        var screenshotPath = Path.Combine(Path.GetTempPath(), $"opus-alpha-smoke-{Guid.NewGuid():N}.bmp");
        try
        {
            screenshot.SaveBmp(screenshotPath);
            new FileInfo(screenshotPath).Length.Should().BeGreaterThan(54);
        }
        finally
        {
            File.Delete(screenshotPath);
        }

        var diagnostics = new D3D12AlphaFrameDiagnostics(
            host.Session.Device.AdapterName,
            host.Session.SwapChain.Width,
            host.Session.SwapChain.Height,
            plan.SceneViewport.Width,
            plan.SceneViewport.Height,
            draws.Count,
            plan.MapInstanceCount,
            stopwatch.Elapsed);
        diagnostics.AdapterName.Should().NotBeNullOrWhiteSpace();
        diagnostics.SubmittedDrawItems.Should().Be(draws.Count);
        diagnostics.CpuFrameTime.Should().BeGreaterThan(TimeSpan.Zero);

        D3D12DebugAssertions.ShouldHaveNoErrors(host.Session.Device);
    }

    private static bool HasVisibleRgb(byte[] rgba) =>
        rgba.Chunk(4).Any(pixel => pixel[0] > 18 || pixel[1] > 18 || pixel[2] > 18);
}
