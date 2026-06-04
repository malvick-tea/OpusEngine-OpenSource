using System;
using System.Numerics;
using FluentAssertions;
using Opus.Engine.Consumer.Assets;
using Opus.Engine.Consumer.Lifecycle;
using Opus.Engine.Consumer.Scene;
using Opus.Engine.Consumer.Telemetry;
using Opus.Foundation;

namespace Opus.Engine.Consumer.Tests;

internal sealed class RecordingConsumer :
    IConsumerSceneSource,
    IConsumerAssetCatalog,
    IConsumerTelemetryProvider,
    IConsumerLifecycleHook
{
    public int StartedCount { get; private set; }

    public int FrameCount { get; private set; }

    public int StoppingCount { get; private set; }

    public int SceneRequests { get; private set; }

    public int AssetRequests { get; private set; }

    public int TelemetryRequests { get; private set; }

    public ConsumerSceneFrame DescribeFrame(ConsumerSceneFrameContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        SceneRequests++;
        return ConsumerContractFixtures.SceneFrame();
    }

    public ConsumerAssetResolution ResolveAsset(ConsumerAssetRequest request)
    {
        AssetRequests++;
        request.Role.Should().Be(ConsumerAssetRole.PrimarySceneModel);
        return ConsumerAssetResolution.Unresolved;
    }

    public ConsumerTelemetrySnapshot CaptureTelemetry(ConsumerTelemetryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        TelemetryRequests++;
        return ConsumerTelemetrySnapshot.Empty;
    }

    public void OnStarted(ConsumerLifecycleStartedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        StartedCount++;
    }

    public void OnFrame(ConsumerFrameContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        FrameCount++;
    }

    public void OnStopping(ConsumerLifecycleStoppingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        StoppingCount++;
    }
}

internal static class ConsumerContractFixtures
{
    public static ConsumerCamera Camera()
    {
        var view = Matrix4x4.CreateLookAt(new Vector3(6f, 4f, 8f), Vector3.Zero, Vector3.UnitY);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3f, 16f / 9f, 0.1f, 500f);
        return new ConsumerCamera(
            view,
            projection,
            new Vector3(6f, 4f, 8f),
            Vector3.Normalize(new Vector3(-6f, -4f, -8f)),
            NearPlane: 0.1f,
            FarPlane: 500f,
            FovYRadians: MathF.PI / 3f,
            AspectRatio: 16f / 9f);
    }

    public static ConsumerSceneFrame SceneFrame()
    {
        var draw = new ConsumerDrawItem(
            ConsumerAssetId.PrimarySceneModel,
            Matrix4x4.Identity,
            Vector4.One);
        return new ConsumerSceneFrame(
            new[] { draw },
            ConsumerCameraSet.SingleMain(Camera()),
            ConsumerLightingSnapshot.SingleSun(Vector3.Normalize(new Vector3(-0.3f, -1f, -0.2f))));
    }

    public static ConsumerFrameContext FrameContext() => ConsumerFrameContext.Create(
        GameTime.AtRate(60).Advance(60),
        TimeSpan.FromMilliseconds(16.7),
        interpolationAlpha: 0.5,
        frameIndex: 7,
        DateTimeOffset.UtcNow);
}
