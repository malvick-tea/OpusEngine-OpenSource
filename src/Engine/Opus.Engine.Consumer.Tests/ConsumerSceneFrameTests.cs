using FluentAssertions;
using Opus.Engine.Consumer.Assets;
using Xunit;

namespace Opus.Engine.Consumer.Tests;

public sealed class ConsumerSceneFrameTests
{
    [Fact]
    public void Scene_frame_preserves_draw_camera_and_lighting_records()
    {
        var scene = ConsumerContractFixtures.SceneFrame();

        scene.DrawItems.Should().ContainSingle();
        scene.DrawItems[0].AssetId.Should().Be(ConsumerAssetId.PrimarySceneModel);
        scene.Cameras.Auxiliary.Should().BeEmpty();
        scene.Lighting.LocalLights.Should().BeEmpty();
    }

    [Fact]
    public void Recording_consumer_implements_all_public_contracts()
    {
        var consumer = new RecordingConsumer();
        var frame = new Opus.Engine.Consumer.Scene.ConsumerSceneFrameContext(
            ConsumerContractFixtures.FrameContext(),
            new Opus.Engine.Consumer.Scene.ConsumerViewportSnapshot(1280, 720, 1024, 600));

        consumer.ResolveAsset(new Opus.Engine.Consumer.Assets.ConsumerAssetRequest(
            Opus.Engine.Consumer.Assets.ConsumerAssetRole.PrimarySceneModel,
            Opus.Engine.Consumer.Assets.ConsumerAssetId.PrimarySceneModel));
        consumer.DescribeFrame(frame);
        consumer.CaptureTelemetry(new Opus.Engine.Consumer.Telemetry.ConsumerTelemetryContext(System.DateTimeOffset.UtcNow));

        consumer.AssetRequests.Should().Be(1);
        consumer.SceneRequests.Should().Be(1);
        consumer.TelemetryRequests.Should().Be(1);
    }
}
