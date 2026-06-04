using System;
using Opus.Engine.Consumer.Assets;
using Opus.Engine.Consumer.Lifecycle;
using Opus.Engine.Consumer.Scene;
using Opus.Engine.Consumer.Telemetry;

namespace Opus.Engine.Consumer.Integration;

/// <summary>
/// Registration facade handed to an Opus alpha host by an external consumer. The facade
/// is renderer-neutral: it carries contracts only, while a concrete host decides how to
/// adapt scene records, asset paths, telemetry, and lifecycle hooks into its backend.
/// </summary>
public sealed record ConsumerIntegration
{
    /// <summary>Creates a validated consumer-registration facade.</summary>
    public ConsumerIntegration(
        IConsumerSceneSource? sceneSource,
        IConsumerAssetCatalog? assetCatalog,
        IConsumerTelemetryProvider? telemetryProvider,
        IReadOnlyList<IConsumerLifecycleHook> lifecycleHooks)
    {
        SceneSource = sceneSource;
        AssetCatalog = assetCatalog;
        TelemetryProvider = telemetryProvider;
        LifecycleHooks = ConsumerContractValidation.CopyRequiredList(lifecycleHooks, nameof(lifecycleHooks));
    }

    /// <summary>Scene source queried once per render frame when supplied.</summary>
    public IConsumerSceneSource? SceneSource { get; }

    /// <summary>Asset resolver queried by the host before it falls back to legacy asset options.</summary>
    public IConsumerAssetCatalog? AssetCatalog { get; }

    /// <summary>Telemetry provider queried by overlay and failure-report surfaces.</summary>
    public IConsumerTelemetryProvider? TelemetryProvider { get; }

    /// <summary>Lifecycle hooks dispatched by the host at start, frame, and stop points.</summary>
    public IReadOnlyList<IConsumerLifecycleHook> LifecycleHooks { get; }

    /// <summary>Empty registration used by tests and host code that want an explicit no-op facade.</summary>
    public static ConsumerIntegration Empty { get; } = new(
        sceneSource: null,
        assetCatalog: null,
        telemetryProvider: null,
        lifecycleHooks: Array.Empty<IConsumerLifecycleHook>());

    /// <summary>Returns whether at least one consumer contract was supplied.</summary>
    public bool HasContracts =>
        SceneSource is not null
        || AssetCatalog is not null
        || TelemetryProvider is not null
        || LifecycleHooks.Count > 0;
}
