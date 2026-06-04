using System;
using System.Collections.Generic;
using Opus.Engine.Consumer;
using Opus.Engine.Consumer.Assets;
using Opus.Engine.Consumer.Integration;
using Opus.Engine.Consumer.Lifecycle;
using Opus.Engine.Consumer.Scene;
using Opus.Engine.Consumer.Telemetry;
using Opus.Engine.Runtime;
using Opus.Foundation;

namespace Opus.Engine.Host.Windows.Direct3D12.Consumer;

/// <summary>
/// Host-side bridge translating the engine-neutral
/// <see cref="ConsumerIntegration"/> surface into D3D12-host lifecycle, scene, asset,
/// and telemetry calls. The bridge is single-responsibility (routing) and isolates
/// every consumer-supplied callback from the render loop: failures are logged with
/// <see cref="ConsumerDiagnosticCodes"/> codes, never propagated past this boundary.
/// </summary>
internal sealed class D3D12ConsumerIntegrationBridge
{
    private static readonly IReadOnlyList<IConsumerLifecycleHook> NoHooks = Array.Empty<IConsumerLifecycleHook>();

    private readonly IConsumerSceneSource? _sceneSource;
    private readonly IConsumerAssetCatalog? _assetCatalog;
    private readonly IConsumerTelemetryProvider? _telemetryProvider;
    private readonly IReadOnlyList<IConsumerLifecycleHook> _lifecycleHooks;
    private readonly ILog _log;

    public D3D12ConsumerIntegrationBridge(ConsumerIntegration? integration, ILog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        _log = log;
        _sceneSource = integration?.SceneSource;
        _assetCatalog = integration?.AssetCatalog;
        _telemetryProvider = integration?.TelemetryProvider;
        _lifecycleHooks = integration?.LifecycleHooks ?? NoHooks;
    }

    /// <summary>True when a consumer-supplied scene source is attached.</summary>
    public bool HasSceneSource => _sceneSource is not null;

    /// <summary>
    /// Resolves the primary scene asset path through the consumer catalog when present,
    /// returning <paramref name="fallbackAssetPath"/> when the catalog is absent, throws,
    /// or returns an unresolved result.
    /// </summary>
    public string? ResolvePrimaryAssetPath(string? fallbackAssetPath)
    {
        if (_assetCatalog is null)
        {
            return fallbackAssetPath;
        }

        ConsumerAssetResolution? resolution;
        try
        {
            resolution = _assetCatalog.ResolveAsset(new ConsumerAssetRequest(
                ConsumerAssetRole.PrimarySceneModel,
                ConsumerAssetId.PrimarySceneModel));
        }
        catch (Exception ex)
        {
            _log.Error(
                $"{ConsumerDiagnosticCodes.AssetCatalogFailed}: consumer asset catalog failed; falling back to legacy asset option.",
                ex);
            return fallbackAssetPath;
        }

        return resolution?.FilePath ?? fallbackAssetPath;
    }

    /// <summary>Dispatches <see cref="IConsumerLifecycleHook.OnStarted"/> to every hook.</summary>
    public void NotifyStarted(ConsumerLifecycleStartedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        DispatchLifecycle(static (hook, ctx) => hook.OnStarted(ctx), context);
    }

    /// <summary>Dispatches <see cref="IConsumerLifecycleHook.OnFrame"/> to every hook.</summary>
    public void NotifyFrame(ConsumerFrameContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        DispatchLifecycle(static (hook, ctx) => hook.OnFrame(ctx), context);
    }

    /// <summary>Dispatches <see cref="IConsumerLifecycleHook.OnStopping"/> to every hook.</summary>
    public void NotifyStopping(ConsumerLifecycleStoppingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        DispatchLifecycle(static (hook, ctx) => hook.OnStopping(ctx), context);
    }

    /// <summary>
    /// Captures a consumer scene frame; returns null when no scene source is attached,
    /// when the scene source threw, or when the source returned a null frame.
    /// </summary>
    public ConsumerSceneFrame? DescribeScene(ConsumerSceneFrameContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_sceneSource is null)
        {
            return null;
        }

        try
        {
            var frame = _sceneSource.DescribeFrame(context);
            if (frame is null)
            {
                _log.Error($"{ConsumerDiagnosticCodes.SceneSourceFailed}: consumer scene source returned null.");
            }

            return frame;
        }
        catch (Exception ex)
        {
            _log.Error($"{ConsumerDiagnosticCodes.SceneSourceFailed}: consumer scene source failed.", ex);
            return null;
        }
    }

    /// <summary>
    /// Captures consumer telemetry; falls back to <see cref="ConsumerTelemetrySnapshot.Empty"/>
    /// when no provider is attached, the provider throws, or the provider returns null.
    /// </summary>
    public ConsumerTelemetrySnapshot CaptureTelemetry(ConsumerTelemetryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_telemetryProvider is null)
        {
            return ConsumerTelemetrySnapshot.Empty;
        }

        try
        {
            return _telemetryProvider.CaptureTelemetry(context) ?? ConsumerTelemetrySnapshot.Empty;
        }
        catch (Exception ex)
        {
            _log.Error($"{ConsumerDiagnosticCodes.TelemetryProviderFailed}: consumer telemetry provider failed.", ex);
            return ConsumerTelemetrySnapshot.Empty;
        }
    }

    /// <summary>Adapts an <see cref="OpusRenderFrame"/> into a consumer frame context.</summary>
    public static ConsumerFrameContext ToConsumerFrameContext(OpusRenderFrame frame) =>
        ConsumerFrameContext.Create(
            frame.Time,
            frame.Delta,
            frame.InterpolationAlpha,
            frame.FrameIndex,
            DateTimeOffset.UtcNow);

    private void DispatchLifecycle<TContext>(Action<IConsumerLifecycleHook, TContext> invoker, TContext context)
        where TContext : class
    {
        var hooks = _lifecycleHooks;
        for (var i = 0; i < hooks.Count; i++)
        {
            var hook = hooks[i];
            try
            {
                invoker(hook, context);
            }
            catch (Exception ex)
            {
                _log.Error(
                    $"{ConsumerDiagnosticCodes.LifecycleHookFailed}: consumer lifecycle hook '{hook.GetType().FullName}' failed.",
                    ex);
            }
        }
    }
}
