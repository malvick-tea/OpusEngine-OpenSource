using System.Linq;
using System.Numerics;
using FluentAssertions;
using Opus.Engine.Direct3D12.Tests.Fixtures;
using Opus.Engine.Pal.Windows.Direct3D12;
using Opus.Engine.Renderer.Direct3D12.Alpha;
using Opus.Engine.Rhi.Direct3D12;
using Opus.Engine.Ui;
using Opus.Engine.Ui.Direct3D12;
using Silk.NET.Direct3D12;
using Xunit;

namespace Opus.Engine.Direct3D12.Tests.Renderer;

/// <summary>Live D3D12 smoke for the hand-drawn annotation surface (ADR-0033): commits a couple of
/// freehand ink strokes into an <see cref="InkAnnotationLayer"/>, draws them through
/// <see cref="D3D12DrawSurface.DrawAnnotations"/> over a dark clear, reads the frame back, and
/// proves the ink rendered as saturated coloured pixels on the path with a clean debug layer.
/// Skips when no D3D12 adapter is available.</summary>
public sealed unsafe class D3D12InkSurfaceSmokeTests
{
    private static readonly Color InkColour = Color.FromRgb(220, 40, 40);

    [SkippableFact]
    public void Annotation_surface_draws_committed_freehand_strokes()
    {
        using var host = D3D12SmokeHost.OpenWindow("opus-d3d12-ink-smoke", width: 640, height: 360);
        var plan = D3D12AlphaFramePlan.Create(host.Session.SwapChain.Width, host.Session.SwapChain.Height);
        using var atlas = host.BuildFontAtlas(plan.UiText);
        using var frameLoop = new D3D12UiFrameLoop(host.Session);
        using var surface = D3D12DrawSurface.Create(
            host.Session.Device,
            atlas,
            host.Session.Compiler,
            host.Session.SwapChain.Format,
            frameSlots: D3D12SwapChain.BufferCount,
            maxQuadsPerFrame: 4096);

        // A zigzag stroke plus a short underline — both committed marks, drawn through the layer
        // exactly as a commander tool would after a drag gesture.
        var annotations = new InkAnnotationLayer();
        annotations.BeginStroke(widthPx: 8f, InkColour);
        foreach (var point in new[]
                 {
                     new Vector2(120, 190), new Vector2(220, 110), new Vector2(320, 210),
                     new Vector2(420, 110), new Vector2(520, 190),
                 })
        {
            annotations.AddPoint(point);
        }

        annotations.EndStroke();
        annotations.BeginStroke(widthPx: 6f, InkColour);
        annotations.AddPoint(new Vector2(160, 260));
        annotations.AddPoint(new Vector2(480, 260));
        annotations.EndStroke();
        annotations.CommittedStrokes.Should().HaveCount(2);

        D3D12DebugAssertions.Clear(host.Session.Device);

        host.Session.Window.PollEvents();
        var frame = frameLoop.BeginFrame();
        surface.BeginFrame(
            frame.CommandList,
            frame.RenderTargetView,
            frame.BackBufferSlot,
            frame.ViewportWidth,
            frame.ViewportHeight);
        surface.Clear(Color.FromRgb(4, 6, 10));
        surface.DrawAnnotations(annotations);
        surface.EndFrame();

        using var readback = D3D12TextureReadback.CreateForCurrentBackBuffer(
            host.Session.Device,
            host.Session.SwapChain,
            "ink-smoke.screenshot");
        readback.RecordCopyFrom(
            frame.CommandList,
            host.Session.SwapChain.CurrentBackBuffer,
            ResourceStates.RenderTarget,
            ResourceStates.RenderTarget);
        frameLoop.EndFrame();
        host.Session.Device.WaitForIdle();

        var screenshot = readback.ReadRgba8();
        HasVisibleRgb(screenshot.Rgba8).Should().BeTrue("the committed ink strokes should render");
        HasSaturatedInk(screenshot.Rgba8).Should()
            .BeTrue("the drawn marks should land as saturated coloured ink, not flat grey");

        D3D12DebugAssertions.ShouldHaveNoErrors(host.Session.Device);
    }

    private static bool HasVisibleRgb(byte[] rgba) =>
        rgba.Chunk(4).Any(pixel => pixel[0] > 18 || pixel[1] > 18 || pixel[2] > 18);

    // Channel-order-agnostic: a saturated ink pixel has a bright dominant channel and a wide spread
    // between its brightest and darkest channel, unlike the near-grey dark clear.
    private static bool HasSaturatedInk(byte[] rgba) =>
        rgba.Chunk(4).Any(pixel =>
        {
            var max = System.Math.Max(pixel[0], System.Math.Max(pixel[1], pixel[2]));
            var min = System.Math.Min(pixel[0], System.Math.Min(pixel[1], pixel[2]));
            return max > 120 && max - min > 80;
        });
}
