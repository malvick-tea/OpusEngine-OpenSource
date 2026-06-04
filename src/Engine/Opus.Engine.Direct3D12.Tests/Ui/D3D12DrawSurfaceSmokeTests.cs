using Opus.Engine.Direct3D12.Tests.Fixtures;
using Opus.Engine.Pal.Windows.Direct3D12;
using Opus.Engine.Ui;
using Opus.Engine.Ui.Direct3D12;
using Opus.Engine.Ui.Direct3D12.Text;
using Xunit;

namespace Opus.Engine.Direct3D12.Tests.Ui;

public sealed class D3D12DrawSurfaceSmokeTests
{
    private static readonly string[] SmokeText = ["OPUS ДВИЖОК"];

    [SkippableFact]
    public void Draw_surface_renders_basic_primitives_and_bilingual_text()
    {
        using var host = D3D12SmokeHost.OpenWindow("opus-d3d12-ui-smoke", width: 256, height: 160);
        using var frameLoop = new D3D12UiFrameLoop(host.Session);
        using var atlas = host.BuildFontAtlas(SmokeText);
        using var surface = D3D12DrawSurface.Create(
            host.Session.Device,
            atlas,
            host.Session.Compiler,
            host.Session.SwapChain.Format,
            frameSlots: 2,
            maxQuadsPerFrame: 256);

        var frame = frameLoop.BeginFrame();
        surface.BeginFrame(
            frame.CommandList,
            frame.RenderTargetView,
            frame.BackBufferSlot,
            frame.ViewportWidth,
            frame.ViewportHeight);
        surface.Clear(Color.Black);
        surface.FillRect(8, 8, 64, 24, Color.FromRgb(40, 90, 180));
        surface.StrokeRect(80, 8, 72, 32, 3, Color.White);
        surface.DrawLine(8, 56, 148, 72, 4, Color.FromRgb(230, 190, 80));
        surface.FillCircle(184, 40, 18, Color.FromRgb(160, 60, 80));
        surface.StrokeCircle(224, 40, 18, 4, Color.FromRgb(80, 180, 140));
        surface.DrawText("OPUS ДВИЖОК", 8, 104, 22, Color.White);
        surface.EndFrame();
        frameLoop.EndFrame();
    }
}
