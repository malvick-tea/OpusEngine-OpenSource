using System;
using System.Numerics;
using FluentAssertions;
using Opus.Engine.Direct3D12.Tests.Fixtures;
using Opus.Engine.Pal.Windows.Direct3D12;
using Opus.Engine.Rhi.Direct3D12;
using Opus.Engine.Ui;
using Opus.Engine.Ui.Direct3D12;
using Opus.Engine.Ui.Text;
using Silk.NET.Direct3D12;
using Xunit;

namespace Opus.Engine.Direct3D12.Tests.Ui;

/// <summary>Live D3D12 smoke for M5.6: the bundled-Roboto atlas (ADR-0034) renders a mixed
/// English + Russian HUD string near the top of the frame, and a world-space-projected Russian
/// label (<see cref="WorldSpaceTextProjector"/>) at the centre — both through the same
/// <see cref="D3D12DrawSurface.DrawText"/> glyph path. Reads the frame back and proves each band
/// painted bright glyph pixels on the dark clear, with a clean debug layer. Skips when no D3D12
/// adapter is available.</summary>
public sealed unsafe class D3D12CyrillicTextSmokeTests
{
    private const int FrameWidth = 512;
    private const int FrameHeight = 256;
    private const int HudFontSize = 40;
    private const int LabelFontSize = 36;
    private const byte BrightChannel = 150;
    private const int MinBandPixels = 40;

    // The atlas corpus is the union of every string the frame draws (Latin + Cyrillic + digits).
    private static readonly string[] AtlasCorpus = ["OPUS Привет, мир! 0123 Метка"];

    [SkippableFact]
    public void Roboto_atlas_renders_latin_cyrillic_hud_and_a_world_space_label()
    {
        using var host = D3D12SmokeHost.OpenWindow("opus-d3d12-text-smoke", FrameWidth, FrameHeight);
        using var atlas = host.BuildFontAtlas(AtlasCorpus);
        using var frameLoop = new D3D12UiFrameLoop(host.Session);
        using var surface = D3D12DrawSurface.Create(
            host.Session.Device,
            atlas,
            host.Session.Compiler,
            host.Session.SwapChain.Format,
            frameSlots: D3D12SwapChain.BufferCount,
            maxQuadsPerFrame: 4096);

        const string hud = "OPUS Привет";   // mixed English + Russian HUD line
        const string worldLabel = "Метка";  // Russian world-space label

        D3D12DebugAssertions.Clear(host.Session.Device);
        host.Session.Window.PollEvents();
        var frame = frameLoop.BeginFrame();

        // World point on the camera axis projects to the centre of the surface, exercising the
        // pure projector through the live glyph path exactly as a consumer label would.
        var view = Matrix4x4.CreateLookAt(new Vector3(0f, 0f, 4f), Vector3.Zero, Vector3.UnitY);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(
            MathF.PI / 3f, frame.ViewportWidth / (float)frame.ViewportHeight, 0.1f, 100f);
        var anchor = WorldSpaceTextProjector.Project(
            Vector3.Zero, view * projection, frame.ViewportWidth, frame.ViewportHeight);
        anchor.Visible.Should().BeTrue("the look-at target must project into the view");

        surface.BeginFrame(
            frame.CommandList, frame.RenderTargetView, frame.BackBufferSlot, frame.ViewportWidth, frame.ViewportHeight);
        surface.Clear(Color.FromRgb(6, 8, 12));
        surface.MeasureText(hud, HudFontSize).Should().BeGreaterThan(0);
        surface.DrawText(hud, 16, 36, HudFontSize, Color.White);
        surface.DrawText(
            worldLabel,
            anchor.CenteredLeft(surface.MeasureText(worldLabel, LabelFontSize)),
            anchor.PixelY,
            LabelFontSize,
            Color.FromRgb(240, 230, 120));
        surface.EndFrame();

        using var readback = D3D12TextureReadback.CreateForCurrentBackBuffer(
            host.Session.Device, host.Session.SwapChain, "text-smoke.screenshot");
        readback.RecordCopyFrom(
            frame.CommandList,
            host.Session.SwapChain.CurrentBackBuffer,
            ResourceStates.RenderTarget,
            ResourceStates.RenderTarget);
        frameLoop.EndFrame();
        host.Session.Device.WaitForIdle();

        var screenshot = readback.ReadRgba8();
        BrightPixelsInRows(screenshot, 30, 90).Should()
            .BeGreaterThan(MinBandPixels, "the EN/RU HUD line must paint bright glyph pixels near the top");
        BrightPixelsInRows(screenshot, anchor.PixelY - 8, anchor.PixelY + LabelFontSize + 8).Should()
            .BeGreaterThan(MinBandPixels, "the world-space label must paint bright glyph pixels at the centre");

        D3D12DebugAssertions.ShouldHaveNoErrors(host.Session.Device);
    }

    // Counts bright pixels (any colour channel above the threshold) in the half-open row band
    // [yStart, yEnd). Rgba8 is tightly packed at Width*4 bytes per row.
    private static int BrightPixelsInRows(D3D12Screenshot screenshot, int yStart, int yEnd)
    {
        var rgba = screenshot.Rgba8;
        var rowBytes = screenshot.Width * 4;
        var first = Math.Max(0, yStart);
        var last = Math.Min(screenshot.Height, yEnd);
        var count = 0;
        for (var y = first; y < last; y++)
        {
            for (var x = 0; x < screenshot.Width; x++)
            {
                var i = (y * rowBytes) + (x * 4);
                if (rgba[i] > BrightChannel || rgba[i + 1] > BrightChannel || rgba[i + 2] > BrightChannel)
                {
                    count++;
                }
            }
        }

        return count;
    }
}
