using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Opus.Engine.Consumer;
using Opus.Engine.Consumer.Assets;
using Opus.Engine.Consumer.Integration;
using Opus.Engine.Consumer.Lifecycle;
using Opus.Engine.Consumer.Scene;
using Opus.Engine.Consumer.Telemetry;
using Opus.Engine.Host.Windows.Direct3D12.Consumer;
using Opus.Foundation;
using Xunit;

namespace Opus.Engine.Host.Windows.Direct3D12.Tests.Consumer;

public sealed class D3D12ConsumerIntegrationBridgeTests
{
    private static readonly ConsumerLifecycleStartedContext StartedContext =
        new(BuildInfo.Current, DateTimeOffset.UtcNow);

    private static readonly ConsumerLifecycleStoppingContext StoppingContext =
        new(BuildInfo.Current, DateTimeOffset.UtcNow, framesObserved: 7);

    private static readonly ConsumerFrameContext FrameContext = ConsumerFrameContext.Create(
        GameTime.AtRate(60),
        TimeSpan.FromMilliseconds(16.7),
        interpolationAlpha: 0.5,
        frameIndex: 1,
        DateTimeOffset.UtcNow);

    [Fact]
    public void Bridge_with_null_integration_is_silent_no_op()
    {
        var log = new CapturingLog();
        var bridge = new D3D12ConsumerIntegrationBridge(integration: null, log);

        bridge.HasSceneSource.Should().BeFalse();
        bridge.ResolvePrimaryAssetPath("fallback.glb").Should().Be("fallback.glb");
        bridge.DescribeScene(new ConsumerSceneFrameContext(FrameContext, BuildViewport())).Should().BeNull();
        bridge.CaptureTelemetry(new ConsumerTelemetryContext(DateTimeOffset.UtcNow))
            .Should().BeSameAs(ConsumerTelemetrySnapshot.Empty);
        bridge.NotifyStarted(StartedContext);
        bridge.NotifyFrame(FrameContext);
        bridge.NotifyStopping(StoppingContext);

        log.Entries.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_primary_asset_path_returns_catalog_value()
    {
        var log = new CapturingLog();
        var catalog = new StubAssetCatalog(new ConsumerAssetResolution("C:/content/runtime.glb"));
        var bridge = new D3D12ConsumerIntegrationBridge(BuildIntegration(catalog: catalog), log);

        var resolved = bridge.ResolvePrimaryAssetPath(fallbackAssetPath: "C:/fallback.glb");

        resolved.Should().NotBeNull();
        resolved!.Should().EndWith("runtime.glb");
        catalog.RequestCount.Should().Be(1);
    }

    [Fact]
    public void Resolve_primary_asset_path_falls_back_when_catalog_throws()
    {
        var log = new CapturingLog();
        var bridge = new D3D12ConsumerIntegrationBridge(
            BuildIntegration(catalog: new ThrowingAssetCatalog()),
            log);

        var resolved = bridge.ResolvePrimaryAssetPath("C:/fallback.glb");

        resolved.Should().Be("C:/fallback.glb");
        log.Entries.Should().Contain(line => line.Contains(ConsumerDiagnosticCodes.AssetCatalogFailed));
    }

    [Fact]
    public void Resolve_primary_asset_path_falls_back_on_unresolved()
    {
        var log = new CapturingLog();
        var bridge = new D3D12ConsumerIntegrationBridge(
            BuildIntegration(catalog: new StubAssetCatalog(ConsumerAssetResolution.Unresolved)),
            log);

        bridge.ResolvePrimaryAssetPath("C:/fallback.glb").Should().Be("C:/fallback.glb");
    }

    [Fact]
    public void Describe_scene_returns_null_and_logs_when_source_throws()
    {
        var log = new CapturingLog();
        var bridge = new D3D12ConsumerIntegrationBridge(
            BuildIntegration(sceneSource: new ThrowingSceneSource()),
            log);

        var frame = bridge.DescribeScene(new ConsumerSceneFrameContext(FrameContext, BuildViewport()));

        frame.Should().BeNull();
        log.Entries.Should().Contain(line => line.Contains(ConsumerDiagnosticCodes.SceneSourceFailed));
    }

    [Fact]
    public void Capture_telemetry_returns_empty_when_provider_throws()
    {
        var log = new CapturingLog();
        var bridge = new D3D12ConsumerIntegrationBridge(
            BuildIntegration(telemetry: new ThrowingTelemetryProvider()),
            log);

        var snapshot = bridge.CaptureTelemetry(new ConsumerTelemetryContext(DateTimeOffset.UtcNow));

        snapshot.Should().BeSameAs(ConsumerTelemetrySnapshot.Empty);
        log.Entries.Should().Contain(line => line.Contains(ConsumerDiagnosticCodes.TelemetryProviderFailed));
    }

    [Fact]
    public void Lifecycle_dispatch_isolates_failures_per_hook()
    {
        var log = new CapturingLog();
        var firstThrower = new ThrowingLifecycleHook();
        var observer = new RecordingLifecycleHook();
        var bridge = new D3D12ConsumerIntegrationBridge(
            BuildIntegration(hooks: new IConsumerLifecycleHook[] { firstThrower, observer }),
            log);

        bridge.NotifyStarted(StartedContext);
        bridge.NotifyFrame(FrameContext);
        bridge.NotifyStopping(StoppingContext);

        firstThrower.StartedCount.Should().Be(1);
        firstThrower.FrameCount.Should().Be(1);
        firstThrower.StoppingCount.Should().Be(1);
        observer.StartedCount.Should().Be(1);
        observer.FrameCount.Should().Be(1);
        observer.StoppingCount.Should().Be(1);
        log.Entries.Count(line => line.Contains(ConsumerDiagnosticCodes.LifecycleHookFailed))
            .Should().Be(3);
    }

    [Fact]
    public void Notify_methods_reject_null_context()
    {
        var bridge = new D3D12ConsumerIntegrationBridge(integration: null, new CapturingLog());

        bridge.Invoking(b => b.NotifyStarted(null!)).Should().Throw<ArgumentNullException>();
        bridge.Invoking(b => b.NotifyFrame(null!)).Should().Throw<ArgumentNullException>();
        bridge.Invoking(b => b.NotifyStopping(null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Bridge_constructor_rejects_null_log()
    {
        Action act = () => _ = new D3D12ConsumerIntegrationBridge(integration: null, log: null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static ConsumerViewportSnapshot BuildViewport() => new(1280, 720, 1024, 600);

    private static ConsumerIntegration BuildIntegration(
        IConsumerSceneSource? sceneSource = null,
        IConsumerAssetCatalog? catalog = null,
        IConsumerTelemetryProvider? telemetry = null,
        IReadOnlyList<IConsumerLifecycleHook>? hooks = null) => new(
        sceneSource: sceneSource,
        assetCatalog: catalog,
        telemetryProvider: telemetry,
        lifecycleHooks: hooks ?? Array.Empty<IConsumerLifecycleHook>());

    private sealed class CapturingLog : ILog
    {
        private readonly List<string> _entries = new();

        public IReadOnlyList<string> Entries => _entries;

        public bool IsEnabled(LogLevel level) => true;

        public void Log(LogLevel level, string message, Exception? exception = null)
        {
            _entries.Add(exception is null ? message : $"{message} :: {exception.GetType().Name}: {exception.Message}");
        }
    }

    private sealed class StubAssetCatalog : IConsumerAssetCatalog
    {
        private readonly ConsumerAssetResolution _resolution;

        public StubAssetCatalog(ConsumerAssetResolution resolution)
        {
            _resolution = resolution;
        }

        public int RequestCount { get; private set; }

        public ConsumerAssetResolution ResolveAsset(ConsumerAssetRequest request)
        {
            RequestCount++;
            return _resolution;
        }
    }

    private sealed class ThrowingAssetCatalog : IConsumerAssetCatalog
    {
        public ConsumerAssetResolution ResolveAsset(ConsumerAssetRequest request)
            => throw new InvalidOperationException("catalog blew up");
    }

    private sealed class ThrowingSceneSource : IConsumerSceneSource
    {
        public ConsumerSceneFrame DescribeFrame(ConsumerSceneFrameContext context)
            => throw new InvalidOperationException("scene blew up");
    }

    private sealed class ThrowingTelemetryProvider : IConsumerTelemetryProvider
    {
        public ConsumerTelemetrySnapshot CaptureTelemetry(ConsumerTelemetryContext context)
            => throw new InvalidOperationException("telemetry blew up");
    }

    private sealed class ThrowingLifecycleHook : IConsumerLifecycleHook
    {
        public int StartedCount { get; private set; }

        public int FrameCount { get; private set; }

        public int StoppingCount { get; private set; }

        public void OnStarted(ConsumerLifecycleStartedContext context)
        {
            StartedCount++;
            throw new InvalidOperationException("OnStarted threw");
        }

        public void OnFrame(ConsumerFrameContext context)
        {
            FrameCount++;
            throw new InvalidOperationException("OnFrame threw");
        }

        public void OnStopping(ConsumerLifecycleStoppingContext context)
        {
            StoppingCount++;
            throw new InvalidOperationException("OnStopping threw");
        }
    }

    private sealed class RecordingLifecycleHook : IConsumerLifecycleHook
    {
        public int StartedCount { get; private set; }

        public int FrameCount { get; private set; }

        public int StoppingCount { get; private set; }

        public void OnStarted(ConsumerLifecycleStartedContext context) => StartedCount++;

        public void OnFrame(ConsumerFrameContext context) => FrameCount++;

        public void OnStopping(ConsumerLifecycleStoppingContext context) => StoppingCount++;
    }
}
