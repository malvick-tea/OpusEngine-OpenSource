using System;
using System.Collections.Generic;
using System.Numerics;
using Opus.Engine.Consumer;
using Opus.Engine.Consumer.Assets;
using Opus.Engine.Consumer.Scene;
using Opus.Engine.Renderer;
using Opus.Engine.Renderer.Direct3D12.Assets;
using Opus.Engine.Renderer.Direct3D12.Scene;
using Opus.Foundation;

namespace Opus.Engine.Host.Windows.Direct3D12.Consumer;

internal static class D3D12ConsumerSceneAdapter
{
    public static IReadOnlyList<SceneNodeDraw> BuildDraws(
        ConsumerSceneFrame scene,
        GarageSceneAssets assets,
        ILog log)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(log);

        // M10 prototype seam: lead to harden — replace primary-template expansion with
        // a real asset-id to GPU-resource catalog once consumer packages expose baked assets.
        var worlds = new List<Matrix4x4>(scene.DrawItems.Count);
        var tints = new List<Vector4>(scene.DrawItems.Count);
        for (var i = 0; i < scene.DrawItems.Count; i++)
        {
            var item = scene.DrawItems[i];
            if (!item.AssetId.Equals(ConsumerAssetId.PrimarySceneModel))
            {
                log.Warn($"{ConsumerDiagnosticCodes.UnsupportedDrawAsset}: unsupported consumer asset id '{item.AssetId}'.");
                continue;
            }

            worlds.Add(item.World);
            tints.Add(item.TintFactor);
        }

        return worlds.Count == 0
            ? Array.Empty<SceneNodeDraw>()
            : SceneNodeDrawTransformer.Instantiate(assets.TankTemplate, worlds, tints);
    }

    public static FrameCameraSet ToFrameCameraSet(ConsumerCameraSet cameras)
    {
        ArgumentNullException.ThrowIfNull(cameras);
        var auxiliary = new CameraSetup[cameras.Auxiliary.Count];
        for (var i = 0; i < cameras.Auxiliary.Count; i++)
        {
            auxiliary[i] = ToCameraSetup(cameras.Auxiliary[i]);
        }

        return new FrameCameraSet(ToCameraSetup(cameras.Main), auxiliary);
    }

    public static LightingSetup ToLightingSetup(ConsumerLightingSnapshot lighting)
    {
        ArgumentNullException.ThrowIfNull(lighting);
        var localLights = new LocalLight[lighting.LocalLights.Count];
        for (var i = 0; i < lighting.LocalLights.Count; i++)
        {
            localLights[i] = ToLocalLight(lighting.LocalLights[i]);
        }

        return new LightingSetup(ToSun(lighting.Sun), localLights, ToSky(lighting.Sky));
    }

    private static CameraSetup ToCameraSetup(ConsumerCamera camera) => new(
        camera.View,
        camera.Projection,
        camera.PositionWorld,
        camera.ForwardWorld,
        camera.NearPlane,
        camera.FarPlane,
        camera.FovYRadians,
        camera.AspectRatio);

    private static DirectionalLight ToSun(ConsumerDirectionalLight sun) => new(
        sun.DirectionWorld,
        sun.Colour,
        sun.Intensity,
        sun.CastsShadows);

    private static SkySetup ToSky(ConsumerSkySnapshot sky) => new(
        sky.SunDirectionWorld,
        sky.ZenithColour,
        sky.HorizonColour,
        sky.ExposureEv,
        sky.EnvironmentMapHandle);

    private static LocalLight ToLocalLight(ConsumerLocalLight light) => new(
        ToLocalLightKind(light.Kind),
        light.PositionWorld,
        light.DirectionWorld,
        light.Colour,
        light.Intensity,
        light.Range,
        light.SpotInnerAngleRadians,
        light.SpotOuterAngleRadians,
        light.CastsShadows);

    private static LocalLightKind ToLocalLightKind(ConsumerLocalLightKind kind) => kind switch
    {
        ConsumerLocalLightKind.Point => LocalLightKind.Point,
        ConsumerLocalLightKind.Spot => LocalLightKind.Spot,
        ConsumerLocalLightKind.Area => LocalLightKind.Area,
        _ => throw new ArgumentOutOfRangeException(
            nameof(kind),
            kind,
            "Unknown consumer local-light kind cannot be mapped to a host local-light kind."),
    };
}
