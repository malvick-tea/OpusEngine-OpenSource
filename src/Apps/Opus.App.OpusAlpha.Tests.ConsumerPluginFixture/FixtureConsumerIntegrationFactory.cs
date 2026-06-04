using System;
using Opus.Engine.Consumer.Integration;
using Opus.Engine.Consumer.Lifecycle;

namespace Opus.App.OpusAlpha.Tests.ConsumerPluginFixture;

/// <summary>
/// The single public <see cref="IConsumerIntegrationFactory"/> in this fixture assembly. It lets
/// the alpha host's <c>ConsumerIntegrationAssemblyLoader</c> be exercised end to end: the loader
/// discovers this factory by reflection after loading the DLL from disk, constructs it, and builds
/// a <see cref="ConsumerIntegration"/>. The integration carries one lifecycle hook so the loaded
/// result reports <see cref="ConsumerIntegration.HasContracts"/> as true.
/// </summary>
public sealed class FixtureConsumerIntegrationFactory : IConsumerIntegrationFactory
{
    /// <inheritdoc />
    public ConsumerIntegration CreateIntegration() => new(
        sceneSource: null,
        assetCatalog: null,
        telemetryProvider: null,
        lifecycleHooks: new IConsumerLifecycleHook[] { new FixtureLifecycleHook() });

    private sealed class FixtureLifecycleHook : IConsumerLifecycleHook
    {
        // No-op hooks: the fixture exists to be discovered and constructed, not to drive a scene.
        // The contexts are validated so the methods are real statements, not empty bodies.
        public void OnStarted(ConsumerLifecycleStartedContext context) => ArgumentNullException.ThrowIfNull(context);

        public void OnFrame(ConsumerFrameContext context) => ArgumentNullException.ThrowIfNull(context);

        public void OnStopping(ConsumerLifecycleStoppingContext context) => ArgumentNullException.ThrowIfNull(context);
    }
}
