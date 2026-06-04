using System;
using System.Collections.Generic;
using System.Linq;
using Opus.Engine.Pal.Windows.Direct3D12;
using Opus.Engine.Renderer.Direct3D12;
using Opus.Engine.Renderer.Direct3D12.Alpha;
using Opus.Engine.Renderer.Direct3D12.Scene;
using Opus.Engine.Rhi.Direct3D12;
using Opus.Engine.Ui.Direct3D12;
using Opus.Engine.Ui.Direct3D12.Text;

namespace Opus.Engine.Host.Windows.Direct3D12.Scene;

/// <summary>Disposable bundle of every GPU object the D3D12 alpha host needs each frame:
/// the offscreen scene viewport, the forward scene renderer, the UI frame loop, the
/// font atlas, and the UI draw surface that composites the scene texture. Constructed
/// once at host start; consumed and disposed once at host stop.
/// <para>
/// The rig also exposes the captured <see cref="D3D12AlphaFramePlan"/> for the
/// application's render loop. <see cref="Resize"/> regenerates the plan and rebuilds the
/// size-dependent GPU objects (the offscreen scene viewport and the forward renderer's
/// HDR/depth targets) for a new back-buffer size, reaching the exact state a rig created
/// at that size would have. The size-agnostic objects — the UI frame loop (reads the swap
/// chain afresh each frame), the font atlas (fixed glyph pixel height), and the draw
/// surface — are preserved across the resize.
/// </para>
/// </summary>
public sealed class D3D12AlphaSceneRig : IDisposable
{
    private const int DefaultMaxQuadsPerFrame = 2048;
    private const string AtlasGlyphCorpus =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ" +
        "abcdefghijklmnopqrstuvwxyz" +
        "0123456789" +
        " .,:;-_/|()[]<>+=";

    private readonly D3D12WindowSession _session;
    private readonly string _namePrefix;
    private readonly D3D12AlphaScenePopulation _population;
    private readonly D3D12UiFrameLoop _frameLoop;
    private readonly D3D12FontAtlas _fontAtlas;
    private readonly D3D12DrawSurface _drawSurface;
    private D3D12SceneViewport _sceneViewport;
    private D3D12ForwardSceneRenderer _sceneRenderer;
    private int _backBufferWidth;
    private int _backBufferHeight;
    private bool _disposed;

    private D3D12AlphaSceneRig(
        D3D12WindowSession session,
        string namePrefix,
        D3D12AlphaScenePopulation population,
        D3D12AlphaFramePlan plan,
        D3D12SceneViewport sceneViewport,
        D3D12ForwardSceneRenderer sceneRenderer,
        D3D12UiFrameLoop frameLoop,
        D3D12FontAtlas fontAtlas,
        D3D12DrawSurface drawSurface)
    {
        _session = session;
        _namePrefix = namePrefix;
        _population = population;
        _backBufferWidth = session.SwapChain.Width;
        _backBufferHeight = session.SwapChain.Height;
        Plan = plan;
        _sceneViewport = sceneViewport;
        _sceneRenderer = sceneRenderer;
        _frameLoop = frameLoop;
        _fontAtlas = fontAtlas;
        _drawSurface = drawSurface;
    }

    public D3D12AlphaFramePlan Plan { get; private set; }

    public D3D12SceneViewport SceneViewport => _sceneViewport;

    public D3D12ForwardSceneRenderer SceneRenderer => _sceneRenderer;

    public D3D12UiFrameLoop FrameLoop => _frameLoop;

    public D3D12FontAtlas FontAtlas => _fontAtlas;

    public D3D12DrawSurface DrawSurface => _drawSurface;

    public static D3D12AlphaSceneRig Create(
        D3D12WindowSession session,
        string namePrefix,
        D3D12AlphaScenePopulation? population = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(namePrefix);
        var actualPopulation = population ?? D3D12AlphaScenePopulation.Default;
        actualPopulation.Validate();

        var plan = D3D12AlphaFramePlan.Create(
            session.SwapChain.Width,
            session.SwapChain.Height,
            opponentColumns: actualPopulation.OpponentColumns,
            opponentRows: actualPopulation.OpponentRows,
            projectileTrails: actualPopulation.ProjectileTrails,
            casings: actualPopulation.Casings);

        D3D12SceneViewport? sceneViewport = null;
        D3D12ForwardSceneRenderer? sceneRenderer = null;
        D3D12UiFrameLoop? frameLoop = null;
        D3D12FontAtlas? fontAtlas = null;
        D3D12DrawSurface? drawSurface = null;
        try
        {
            sceneViewport = new D3D12SceneViewport(
                session.Device,
                session.SwapChain,
                plan.SceneViewport.Width,
                plan.SceneViewport.Height,
                $"{namePrefix}.viewport");
            sceneRenderer = new D3D12ForwardSceneRenderer(
                session.Device,
                session.Compiler,
                sceneViewport.Target.Format,
                plan.SceneViewport.Width,
                plan.SceneViewport.Height,
                $"{namePrefix}.forward");
            frameLoop = new D3D12UiFrameLoop(session);
            fontAtlas = D3D12FontAtlas.BuildAndUpload(
                session.Device,
                BuildAtlasGlyphCorpus(plan.UiText),
                pixelHeight: D3D12FontAtlas.DefaultPixelHeight,
                atlasSize: D3D12FontAtlas.DefaultAtlasSize);
            drawSurface = D3D12DrawSurface.Create(
                session.Device,
                fontAtlas,
                session.Compiler,
                session.SwapChain.Format,
                frameSlots: D3D12SwapChain.BufferCount,
                maxQuadsPerFrame: DefaultMaxQuadsPerFrame);

            var rig = new D3D12AlphaSceneRig(
                session, namePrefix, actualPopulation, plan, sceneViewport, sceneRenderer, frameLoop, fontAtlas, drawSurface);
            sceneViewport = null;
            sceneRenderer = null;
            frameLoop = null;
            fontAtlas = null;
            drawSurface = null;
            return rig;
        }
        finally
        {
            drawSurface?.Dispose();
            fontAtlas?.Dispose();
            frameLoop?.Dispose();
            sceneRenderer?.Dispose();
            sceneViewport?.Dispose();
        }
    }

    /// <summary>Regenerates the alpha-frame plan for <paramref name="backBufferWidth"/> ×
    /// <paramref name="backBufferHeight"/> and rebuilds the size-dependent GPU objects (the
    /// offscreen scene viewport and the forward renderer's HDR/depth targets) to match —
    /// the result is identical to a rig freshly created at that size. The deterministic
    /// scene population (opponent grid, transients) is unchanged; only the viewport rect and
    /// camera aspect move. Drains the GPU before touching resources. No-op when disposed, on
    /// an unchanged size, or below the alpha-frame minimum (the host clamps minimised
    /// windows to a no-op before reaching here).</summary>
    public void Resize(int backBufferWidth, int backBufferHeight)
    {
        if (_disposed
            || backBufferWidth < D3D12AlphaFramePlan.MinimumBackBufferWidth
            || backBufferHeight < D3D12AlphaFramePlan.MinimumBackBufferHeight
            || (backBufferWidth == _backBufferWidth && backBufferHeight == _backBufferHeight))
        {
            return;
        }

        _session.Device.WaitForIdle();
        var plan = D3D12AlphaFramePlan.Create(
            backBufferWidth,
            backBufferHeight,
            opponentColumns: _population.OpponentColumns,
            opponentRows: _population.OpponentRows,
            projectileTrails: _population.ProjectileTrails,
            casings: _population.Casings);

        _sceneViewport.Resize(plan.SceneViewport.Width, plan.SceneViewport.Height);
        _sceneRenderer.Resize(plan.SceneViewport.Width, plan.SceneViewport.Height);
        Plan = plan;
        _backBufferWidth = backBufferWidth;
        _backBufferHeight = backBufferHeight;
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
        _sceneRenderer.Dispose();
        _sceneViewport.Dispose();
        _disposed = true;
    }

    private static IEnumerable<string> BuildAtlasGlyphCorpus(IReadOnlyList<string> planText)
    {
        return planText.Concat(new[] { AtlasGlyphCorpus });
    }
}
