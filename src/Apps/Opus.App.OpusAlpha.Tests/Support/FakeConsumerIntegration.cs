using System;
using System.Numerics;
using Opus.Engine.Consumer.Assets;
using Opus.Engine.Consumer.Integration;
using Opus.Engine.Consumer.Lifecycle;
using Opus.Engine.Consumer.Scene;
using Opus.Engine.Consumer.Telemetry;

namespace Opus.App.OpusAlpha.Tests.Support;

internal sealed class FakeConsumerIntegration :
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

    public ConsumerIntegration ToIntegration() => new(
        sceneSource: this,
        assetCatalog: this,
        telemetryProvider: this,
        lifecycleHooks: new IConsumerLifecycleHook[] { this });

    public ConsumerSceneFrame DescribeFrame(ConsumerSceneFrameContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        SceneRequests++;
        var aspect = context.Viewport.SceneViewportWidth / (float)context.Viewport.SceneViewportHeight;
        var camera = BuildCamera(aspect);
        var draw = new ConsumerDrawItem(
            ConsumerAssetId.PrimarySceneModel,
            Matrix4x4.CreateScale(5f),
            Vector4.One);
        return new ConsumerSceneFrame(
            new[] { draw },
            ConsumerCameraSet.SingleMain(camera),
            ConsumerLightingSnapshot.SingleSun(Vector3.Normalize(new Vector3(-0.35f, -1f, -0.2f))));
    }

    public ConsumerAssetResolution ResolveAsset(ConsumerAssetRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        AssetRequests++;
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

    private static ConsumerCamera BuildCamera(float aspect)
    {
        var position = new Vector3(24f, 16f, 34f);
        var target = Vector3.Zero;
        return new ConsumerCamera(
            Matrix4x4.CreateLookAt(position, target, Vector3.UnitY),
            Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3.2f, aspect, 0.1f, 500f),
            position,
            Vector3.Normalize(target - position),
            NearPlane: 0.1f,
            FarPlane: 500f,
            FovYRadians: MathF.PI / 3.2f,
            AspectRatio: aspect);
    }
}
