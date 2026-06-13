using System;
using Opus.Editor.Ui;
using Opus.Engine.Pal.Windows.Direct3D12;
using Opus.Engine.Rhi.Direct3D12;
using Opus.Engine.Ui.Direct3D12;
using Opus.Engine.Ui.Direct3D12.Text;
using Silk.NET.Direct3D12;

namespace Opus.Editor.Direct3D12;

/// <summary>
/// The editor window's GPU surface: the UI frame loop, the glyph atlas, and the 2D draw surface over a live
/// <see cref="D3D12WindowSession"/>, plus the <see cref="EditorFrameDrawer"/>. Renders one
/// <see cref="EditorFrameView"/> per frame through the existing UI draw surface — the editor draws the 3D
/// scene as projected world-space lines on this 2D surface, so it needs no offscreen render target, forward
/// renderer, or new pipeline state (one render path, ADR-0028 / ADR-0033). UI-only by design: this is
/// deliberately not <c>D3D12AlphaSceneRig</c>, which carries the tank sample scene the editor does not use.
/// </summary>
public sealed class EditorRenderSurface : IDisposable
{
    private const int MaxQuadsPerFrame = 8192;
    private const string ScreenshotDebugName = "opus-editor.screenshot";

    // The glyph set the editor bakes: ASCII printable plus the full Cyrillic block, so EN / RU document
    // names, asset references, and the pseudo-code mirror all render. The bundled Roboto face covers
    // Latin + Cyrillic deterministically across machines (ADR-0034); 1024² at 24px has ample room for it.
    private const string AtlasGlyphCorpus =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789" +
        " .,:;-_/|\\()[]{}<>+=*\"'#%&@!?" +
        "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя";

    private readonly D3D12WindowSession _session;
    private readonly D3D12UiFrameLoop _frameLoop;
    private readonly D3D12FontAtlas _fontAtlas;
    private readonly D3D12DrawSurface _drawSurface;
    private readonly EditorFrameDrawer _drawer = new();
    private bool _disposed;

    private EditorRenderSurface(
        D3D12WindowSession session,
        D3D12UiFrameLoop frameLoop,
        D3D12FontAtlas fontAtlas,
        D3D12DrawSurface drawSurface)
    {
        _session = session;
        _frameLoop = frameLoop;
        _fontAtlas = fontAtlas;
        _drawSurface = drawSurface;
    }

    public int Width => _session.SwapChain.Width;

    public int Height => _session.SwapChain.Height;

    /// <summary>Builds the editor's GPU surface over an already-open window session. The session's
    /// lifetime is owned by the caller; this owns the frame loop, atlas, and draw surface it creates and
    /// releases them on <see cref="Dispose"/>.</summary>
    public static EditorRenderSurface Create(D3D12WindowSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        D3D12UiFrameLoop? frameLoop = null;
        D3D12FontAtlas? fontAtlas = null;
        D3D12DrawSurface? drawSurface = null;
        try
        {
            frameLoop = new D3D12UiFrameLoop(session);
            fontAtlas = D3D12FontAtlas.BuildAndUpload(
                session.Device,
                new[] { AtlasGlyphCorpus },
                pixelHeight: D3D12FontAtlas.DefaultPixelHeight,
                atlasSize: D3D12FontAtlas.DefaultAtlasSize);
            drawSurface = D3D12DrawSurface.Create(
                session.Device,
                fontAtlas,
                session.Compiler,
                session.SwapChain.Format,
                frameSlots: D3D12SwapChain.BufferCount,
                maxQuadsPerFrame: MaxQuadsPerFrame);

            var surface = new EditorRenderSurface(session, frameLoop, fontAtlas, drawSurface);
            frameLoop = null;
            fontAtlas = null;
            drawSurface = null;
            return surface;
        }
        finally
        {
            drawSurface?.Dispose();
            fontAtlas?.Dispose();
            frameLoop?.Dispose();
        }
    }

    /// <summary>Renders one editor frame and presents it.</summary>
    public void RenderFrame(EditorFrameView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        ThrowIfDisposed();

        var frame = _frameLoop.BeginFrame();
        DrawScene(frame, view);
        _frameLoop.EndFrame();
    }

    /// <summary>Renders one editor frame, captures the presented back buffer as an RGBA8 screenshot
    /// (optionally written to <paramref name="outputPath"/> as a PNG), and presents the frame. Used by the
    /// live smoke to verify the window renders, and available as the window's screenshot path.</summary>
    public unsafe D3D12Screenshot RenderFrameAndCapture(EditorFrameView view, string? outputPath = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        ThrowIfDisposed();

        var frame = _frameLoop.BeginFrame();
        DrawScene(frame, view);

        using var readback = D3D12TextureReadback.CreateForCurrentBackBuffer(
            _session.Device, _session.SwapChain, ScreenshotDebugName);
        readback.RecordCopyFrom(
            frame.CommandList,
            _session.SwapChain.CurrentBackBuffer,
            ResourceStates.RenderTarget,
            ResourceStates.RenderTarget);

        _frameLoop.EndFrame();
        _session.Device.WaitForIdle();

        var screenshot = readback.ReadRgba8();
        if (outputPath is not null)
        {
            screenshot.SavePng(outputPath);
        }

        return screenshot;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _drawSurface.Dispose();
        _fontAtlas.Dispose();
        _frameLoop.Dispose();
        _disposed = true;
    }

    private void DrawScene(D3D12UiFrame frame, EditorFrameView view)
    {
        _drawSurface.BeginFrame(
            frame.CommandList,
            frame.RenderTargetView,
            frame.BackBufferSlot,
            frame.ViewportWidth,
            frame.ViewportHeight);
        _drawer.Draw(_drawSurface, view);
        _drawSurface.EndFrame();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
