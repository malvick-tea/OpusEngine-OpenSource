using System;
using FluentAssertions;
using Opus.Engine.Consumer.Assets;
using Opus.Engine.Consumer.Integration;
using Opus.Engine.Consumer.Lifecycle;
using Opus.Engine.Consumer.Scene;
using Opus.Engine.Consumer.Telemetry;
using Xunit;

namespace Opus.Engine.Consumer.Tests;

public sealed class ConsumerIntegrationTests
{
    [Fact]
    public void Empty_integration_has_no_contracts()
    {
        ConsumerIntegration.Empty.HasContracts.Should().BeFalse();
        ConsumerIntegration.Empty.LifecycleHooks.Should().BeEmpty();
    }

    [Fact]
    public void Integration_accepts_all_four_consumer_contracts()
    {
        var consumer = new RecordingConsumer();
        var integration = new ConsumerIntegration(
            consumer,
            consumer,
            consumer,
            new IConsumerLifecycleHook[] { consumer });

        integration.HasContracts.Should().BeTrue();
        integration.SceneSource.Should().BeSameAs(consumer);
        integration.AssetCatalog.Should().BeSameAs(consumer);
        integration.TelemetryProvider.Should().BeSameAs(consumer);
        integration.LifecycleHooks.Should().ContainSingle().Which.Should().BeSameAs(consumer);
    }

    [Fact]
    public void Integration_rejects_null_lifecycle_hook_entries()
    {
        var hooks = new IConsumerLifecycleHook[1];
        var act = () => new ConsumerIntegration(
            sceneSource: null,
            assetCatalog: null,
            telemetryProvider: null,
            lifecycleHooks: hooks);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Asset_id_rejects_empty_value()
    {
        var act = () => new ConsumerAssetId(" ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Asset_resolution_rejects_empty_resolved_path()
    {
        var act = () => new ConsumerAssetResolution(string.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Scene_frame_rejects_null_draw_list()
    {
        var act = () => new ConsumerSceneFrame(
            drawItems: null!,
            ConsumerCameraSet.SingleMain(ConsumerContractFixtures.Camera()),
            ConsumerLightingSnapshot.SingleSun(System.Numerics.Vector3.UnitY));

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Telemetry_snapshot_rejects_empty_failure_report_line()
    {
        var act = () => new ConsumerTelemetrySnapshot(
            network: null,
            overlayPanels: Array.Empty<Opus.Engine.Diagnostics.Overlay.DiagnosticPanel>(),
            failureReportLines: new[] { " " });

        act.Should().Throw<ArgumentException>();
    }
}
