using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Opus.Engine.AlphaHarness.Scenes;
using Opus.Engine.Consumer.Lifecycle;
using Opus.Engine.Consumer.Scene;
using Opus.Engine.Diagnostics.Overlay;
using Opus.Engine.Diagnostics.Reports;
using Opus.Engine.Host.Windows.Direct3D12.Consumer;
using Opus.Engine.Host.Windows.Direct3D12.Diagnostics;
using Opus.Engine.Host.Windows.Direct3D12.Frame;
using Opus.Engine.Host.Windows.Direct3D12.Scene;
using Opus.Engine.Host.Windows.Direct3D12.Screenshot;
using Opus.Engine.Pal.Windows.Direct3D12;
using Opus.Engine.Renderer.Direct3D12.Alpha;
using Opus.Engine.Renderer.Direct3D12.Assets;
using Opus.Engine.Renderer.Direct3D12.Scene;
using Opus.Engine.Rhi.Direct3D12;
using Opus.Engine.Runtime;
using Opus.Engine.Ui;
using Opus.Engine.Ui.Direct3D12;
using Opus.Foundation;
using Silk.NET.Direct3D12;

namespace Opus.Engine.Host.Windows.Direct3D12;

/// <summary>Runtime <see cref="IOpusApplication"/> for the Windows/D3D12 host. Drives
/// the canonical Opus 0.1 alpha frame each render tick: offscreen scene viewport →
/// forward scene renderer → UI textured composite → swap-chain present, with optional
/// per-frame readback for a metadata-tagged PNG screenshot.
/// <para>
/// The application owns the GPU rig (<see cref="D3D12AlphaSceneRig"/>) and the loaded
/// assets (<see cref="D3D12AlphaSampleAssets"/>); the surrounding
/// <see cref="D3D12WindowSession"/> is constructed and disposed by the host builder so
/// device/swap-chain teardown is sequenced correctly relative to the SDL window.
/// Overlay rate-limit state and the screenshot request slot live in dedicated
/// collaborators (<see cref="D3D12DiagnosticOverlayCoordinator"/> and
/// <see cref="D3D12ScreenshotRequestQueue"/>) so this class stays focused on the frame
/// orchestration responsibility.
/// </para>
/// </summary>
public sealed unsafe partial class D3D12OpusApplication : IOpusApplication, IDisposable
{
    private const string ScreenshotDebugName = "alpha-host.screenshot";

    private static readonly Vector4 PlayerTint = new(0.92f, 1.0f, 0.74f, 1f);
    private static readonly Color BackgroundColor = Color.FromRgb(4, 6, 10);
    private static readonly Color TopBarColor = Color.FromRgb(18, 24, 34);
    private static readonly Color SceneFrameColor = Color.FromRgb(88, 142, 208);

    private readonly D3D12WindowSession _session;
    private readonly D3D12OpusApplicationOptions _options;
    private readonly ILog _log;
    private readonly D3D12AlphaSampleAssets _assetBundle;
    private readonly D3D12AlphaSceneRig _rig;
    private readonly D3D12AlphaFrameMetrics _metrics;
    private readonly D3D12DiagnosticOverlayCoordinator _overlay;
    private readonly D3D12ConsumerIntegrationBridge _consumer;
    private readonly D3D12AlphaFrameBudgetWatchdog? _budgetWatchdog;
    private readonly D3D12ScreenshotRequestQueue _screenshots = new();
    private readonly D3D12ScreenshotHotkeyLatch _screenshotHotkey = new();
    private readonly string _screenshotsDirectory;
    private readonly D3D12WindowResizeBridge _resizeBridge;
    private bool _disposed;

    public D3D12OpusApplication(
        D3D12WindowSession session,
        D3D12OpusApplicationOptions options,
        ILog log)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(log);
        options.Validate();

        _session = session;
        _options = options;
        _log = log;
        _metrics = new D3D12AlphaFrameMetrics(options.MetricsWindow);
        _overlay = new D3D12DiagnosticOverlayCoordinator(options.EffectiveDiagnosticOverlayOptions);
        _consumer = new D3D12ConsumerIntegrationBridge(options.ConsumerIntegration, log);
        _screenshotsDirectory = OpusDiagnosticsPaths.ScreenshotsDirectory(options.EffectiveDiagnosticsDirectory);
        var budgetPolicy = options.EffectiveFrameBudget;
        _budgetWatchdog = budgetPolicy.Enabled ? new D3D12AlphaFrameBudgetWatchdog(budgetPolicy, log) : null;

        D3D12AlphaSampleAssets? assets = null;
        D3D12AlphaSceneRig? rig = null;
        try
        {
            var assetPath = _consumer.ResolvePrimaryAssetPath(options.AssetPath);
            assets = D3D12AlphaSampleAssets.Load(session.Device, "alpha-host", assetPath);
            rig = D3D12AlphaSceneRig.Create(session, "alpha-host", ResolvePopulation(options.SceneScale));
            _assetBundle = assets;
            _rig = rig;
            assets = null;
            rig = null;
        }
        finally
        {
            rig?.Dispose();
            assets?.Dispose();
        }

        SubscribeWindowHotkeys();
        _resizeBridge = new D3D12WindowResizeBridge(_session.Window, Resize);
    }

    public D3D12AlphaFrameMetrics Metrics => _metrics;

    public D3D12WindowSession Session => _session;

    public D3D12AlphaFramePlan Plan => _rig.Plan;

    public string? LastScreenshotPath => _screenshots.LastSuccessfulPath;

    /// <summary>Queues a metadata-tagged PNG screenshot for the next render frame.
    /// Idempotent within a single frame: calling again before <see cref="Render"/>
    /// observes overwrites the queued path. After <see cref="Dispose"/> the call is a
    /// silent no-op so shutdown hooks cannot crash on a torn-down host.</summary>
    public void RequestScreenshot(string outputPath)
    {
        if (_disposed)
        {
            return;
        }

        _screenshots.Request(outputPath);
    }

    public void OnStarted(OpusHostContext context)
    {
        _log.Info(BuildInfo.Current.ToBannerLine());
        _log.Info(string.Create(
            CultureInfo.InvariantCulture,
            $"D3D12 adapter: {_session.Device.AdapterName} ({_session.SwapChain.Width}x{_session.SwapChain.Height})"));
        _log.Info(string.Create(
            CultureInfo.InvariantCulture,
            $"Asset source: {_assetBundle.SourcePath} ({(_assetBundle.IsProceduralFallback ? "procedural" : "supplied")})"));
        _consumer.NotifyStarted(new ConsumerLifecycleStartedContext(BuildInfo.Current, DateTimeOffset.UtcNow));
    }

    public void FixedTick(GameTime time)
    {
        // Reserved for fixed-tick simulation work. The alpha frame is purely
        // render-driven; deterministic ticks belong to game-side IOpusApplication
        // implementations, not to the engine sample host.
    }

    public void Render(OpusRenderFrame frame)
    {
        ThrowIfDisposed();

        var consumerFrameContext = D3D12ConsumerIntegrationBridge.ToConsumerFrameContext(frame);
        _consumer.NotifyFrame(consumerFrameContext);
        var consumerScene = CaptureConsumerScene(consumerFrameContext);
        var sceneInstanceCount = consumerScene?.DrawItems.Count ?? _rig.Plan.MapInstanceCount;

        var stopwatch = Stopwatch.StartNew();
        var draws = BuildDraws(consumerScene);
        RenderSceneIntoViewport(draws, consumerScene);

        var uiFrame = _rig.FrameLoop.BeginFrame();
        DrawAlphaUi(uiFrame, draws.Count, sceneInstanceCount, frame.FrameIndex);

        var screenshotPath = _screenshots.TryDequeue();
        D3D12TextureReadback? readback = null;
        if (screenshotPath is not null)
        {
            readback = D3D12TextureReadback.CreateForCurrentBackBuffer(
                _session.Device, _session.SwapChain, ScreenshotDebugName);
            readback.RecordCopyFrom(
                uiFrame.CommandList,
                _session.SwapChain.CurrentBackBuffer,
                ResourceStates.RenderTarget,
                ResourceStates.RenderTarget);
        }

        _rig.FrameLoop.EndFrame();

        if (readback is not null && screenshotPath is not null)
        {
            try
            {
                FinaliseScreenshot(readback, screenshotPath, frame.FrameIndex);
            }
            finally
            {
                readback.Dispose();
            }
        }

        stopwatch.Stop();
        _metrics.Record(BuildDiagnostics(stopwatch.Elapsed, draws.Count, sceneInstanceCount));
        _budgetWatchdog?.RecordFrame(stopwatch.Elapsed);
    }

    public void OnPaused(OpusHostContext context) => _log.Info("Alpha host paused.");

    public void OnResumed(OpusHostContext context) => _log.Info("Alpha host resumed.");

    public void OnStopping(OpusHostContext context)
    {
        var snapshot = _metrics.Snapshot();
        _consumer.NotifyStopping(new ConsumerLifecycleStoppingContext(
            BuildInfo.Current,
            DateTimeOffset.UtcNow,
            snapshot.TotalFramesObserved));
        _log.Info(string.Create(
            CultureInfo.InvariantCulture,
            $"Stopping after {snapshot.TotalFramesObserved} frames. CPU frame time over last {snapshot.SampleCount} samples — mean {snapshot.Mean.TotalMilliseconds:F2} ms, p95 {snapshot.P95.TotalMilliseconds:F2} ms, max {snapshot.Max.TotalMilliseconds:F2} ms."));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        UnsubscribeWindowHotkeys();
        _resizeBridge.Dispose();
        _rig.Dispose();
        _assetBundle.Dispose();
        _disposed = true;
    }

    private IReadOnlyList<SceneNodeDraw> BuildDraws(ConsumerSceneFrame? consumerScene)
    {
        if (consumerScene is not null)
        {
            return D3D12ConsumerSceneAdapter.BuildDraws(consumerScene, _assetBundle.Assets, _log);
        }

        var plan = _rig.Plan;
        var assets = _assetBundle.Assets;
        var tankInputs = new GarageSceneDrawBuilder.TankInstancesInput(
            Matrix4x4.CreateScale(6f),
            PlayerTint,
            plan.OpponentTanks,
            plan.OpponentTints);
        var transientInputs = new GarageSceneDrawBuilder.TransientsInput(
            plan.ProjectileTrails,
            assets.ProjectileMeshIndex,
            ShellTemplate: null,
            ShellHeads: null,
            plan.Casings,
            assets.CasingMeshIndex);
        return GarageSceneDrawBuilder.Build(assets.TankTemplate, assets.StaticDraws, in tankInputs, in transientInputs);
    }

    private void RenderSceneIntoViewport(IReadOnlyList<SceneNodeDraw> draws, ConsumerSceneFrame? consumerScene)
    {
        var plan = _rig.Plan;
        var cameras = consumerScene is null
            ? plan.Cameras
            : D3D12ConsumerSceneAdapter.ToFrameCameraSet(consumerScene.Cameras);
        var lighting = consumerScene is null
            ? plan.Lighting
            : D3D12ConsumerSceneAdapter.ToLightingSetup(consumerScene.Lighting);
        _rig.SceneRenderer.Render(
            _rig.SceneViewport.Renderer,
            _assetBundle.Assets.GpuScene,
            draws,
            _assetBundle.Assets.Atlas,
            cameras,
            lighting,
            plan.PostFx,
            _rig.SceneViewport.CreateRenderTargetDescriptor(),
            _assetBundle.Assets.MeshLocalBounds);
    }

    private void DrawAlphaUi(D3D12UiFrame uiFrame, int drawCount, int sceneInstanceCount, ulong frameIndex)
    {
        var surface = _rig.DrawSurface;
        var plan = _rig.Plan;

        surface.BeginFrame(
            uiFrame.CommandList,
            uiFrame.RenderTargetView,
            uiFrame.BackBufferSlot,
            uiFrame.ViewportWidth,
            uiFrame.ViewportHeight);
        surface.Clear(BackgroundColor);
        surface.FillRect(0, 0, uiFrame.ViewportWidth, 30, TopBarColor);
        surface.DrawText(plan.UiText[0], 12, 7, 18, Color.White);
        surface.DrawTexturedRect(
            _rig.SceneViewport.Target.SrvTable,
            _rig.SceneViewport.Target.SrvHeap,
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
            SceneFrameColor);

        var bottomText = string.Create(
            CultureInfo.InvariantCulture,
            $"frame {frameIndex} | draws {drawCount} | scene {sceneInstanceCount}");
        surface.DrawText(bottomText, 14, uiFrame.ViewportHeight - 24, 16, Color.White);
        DrawDiagnosticsOverlay(surface, drawCount, sceneInstanceCount);
        surface.EndFrame();
    }

    private void DrawDiagnosticsOverlay(D3D12DrawSurface surface, int drawCount, int sceneInstanceCount)
    {
        if (!_overlay.ShouldDraw)
        {
            return;
        }

        if (_overlay.ShouldRefresh)
        {
            _overlay.Refresh(BuildOverlayInputs(drawCount, sceneInstanceCount));
        }

        _overlay.Draw(surface);
    }

    private DiagnosticOverlayInputs BuildOverlayInputs(int drawCount, int sceneInstanceCount)
    {
        var plan = _rig.Plan;
        var content = DiagnosticContentSnapshot.Create(
            drawCount,
            sceneInstanceCount,
            _assetBundle.SourcePath,
            _assetBundle.IsProceduralFallback);
        var consumerTelemetry = CaptureConsumerTelemetry();
        return DiagnosticOverlayInputs.Create(
            BuildInfo.Current,
            D3D12DiagnosticSnapshots.ToDiagnosticFrameMetrics(_metrics.Snapshot()),
            D3D12DiagnosticSnapshots.ToDiagnosticAdapter(_session, plan),
            content,
            ResolveNetworkSnapshot(consumerTelemetry),
            LastScreenshotPath,
            DateTimeOffset.UtcNow,
            consumerTelemetry.OverlayPanels);
    }

    private void FinaliseScreenshot(D3D12TextureReadback readback, string path, ulong frameIndex)
    {
        _session.Device.WaitForIdle();
        var screenshot = readback.ReadRgba8();
        var metadata = D3D12HostScreenshotMetadata.Build(
            BuildInfo.Current,
            _session.Device.AdapterName,
            D3D12DiagnosticSnapshots.ToDiagnosticAdapterHardware(_session.Device.AdapterInfo),
            frameIndex,
            DateTimeOffset.UtcNow);
        screenshot.SavePng(path, metadata);
        _screenshots.MarkCompleted(path);
        _log.Info($"Screenshot saved: {path}");
    }

    private D3D12AlphaFrameDiagnostics BuildDiagnostics(TimeSpan elapsed, int drawCount, int sceneInstanceCount)
    {
        var plan = _rig.Plan;
        return new D3D12AlphaFrameDiagnostics(
            _session.Device.AdapterName,
            _session.SwapChain.Width,
            _session.SwapChain.Height,
            plan.SceneViewport.Width,
            plan.SceneViewport.Height,
            drawCount,
            sceneInstanceCount,
            elapsed);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private static D3D12AlphaScenePopulation ResolvePopulation(AlphaSceneScale scale)
    {
        var profile = AlphaSceneScaleProfile.For(scale);
        return new D3D12AlphaScenePopulation(
            profile.OpponentColumns,
            profile.OpponentRows,
            profile.ProjectileTrails,
            profile.Casings);
    }
}
