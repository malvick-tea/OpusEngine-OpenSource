using System;
using System.Collections.Generic;
using Opus.Engine.Pal.Windows.Direct3D12;
using Opus.Engine.Rhi.Direct3D12;
using Opus.Engine.Ui.Direct3D12.Text;
using Xunit;

namespace Opus.Engine.Direct3D12.Tests.Fixtures;

internal sealed class D3D12SmokeHost : IDisposable
{
    private D3D12SmokeHost(D3D12WindowSession session)
    {
        Session = session;
    }

    public D3D12WindowSession Session { get; }

    public static D3D12RhiDevice OpenDevice()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "D3D12 is Windows-only.");
        var device = D3D12RhiDevice.TryCreate(enableDebugLayer: false);
        if (device is null)
        {
            Skip.If(true, "No D3D12-capable adapter is available on this host.");
            throw new InvalidOperationException("Unreachable after Skip.If.");
        }

        return device;
    }

    public static D3D12SmokeHost OpenWindow(string title, int width = 192, int height = 128)
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "D3D12 window smoke tests are Windows-only.");
        var options = D3D12WindowSessionOptions.Windowed(title, width, height, enableDebugLayer: false);
        var session = D3D12WindowSession.TryOpen(options);
        if (session is null)
        {
            Skip.If(true, "SDL video, D3D12 adapter, swap-chain creation, or DXC is unavailable on this host.");
            throw new InvalidOperationException("Unreachable after Skip.If.");
        }

        return new D3D12SmokeHost(session);
    }

    public D3D12FontAtlas BuildFontAtlas(IEnumerable<string> localizedText)
    {
        try
        {
            return D3D12FontAtlas.BuildAndUpload(
                Session.Device,
                localizedText,
                pixelHeight: D3D12FontAtlas.DefaultPixelHeight,
                atlasSize: D3D12FontAtlas.DefaultAtlasSize);
        }
        catch (InvalidOperationException ex)
        {
            Skip.If(true, ex.Message);
            throw new InvalidOperationException("Unreachable after Skip.If.", ex);
        }
        catch (GlyphAtlasOverflowException ex)
        {
            Skip.If(true, ex.Message);
            throw new InvalidOperationException("Unreachable after Skip.If.", ex);
        }
    }

    public void Dispose()
    {
        Session.Dispose();
    }
}
