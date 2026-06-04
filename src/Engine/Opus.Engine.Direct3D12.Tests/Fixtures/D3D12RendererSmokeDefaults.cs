using System;
using System.Numerics;
using Opus.Engine.Renderer;

namespace Opus.Engine.Direct3D12.Tests.Fixtures;

internal static class D3D12RendererSmokeDefaults
{
    public static FrameCameraSet Cameras { get; } = FrameCameraSet.SingleMain(new CameraSetup(
        Matrix4x4.Identity,
        Matrix4x4.Identity,
        Vector3.Zero,
        -Vector3.UnitZ,
        NearPlane: 0.1f,
        FarPlane: 1_000f,
        FovYRadians: MathF.PI / 3f,
        AspectRatio: 16f / 9f));

    public static LightingSetup Lighting { get; } = new(
        new DirectionalLight(-Vector3.UnitY, Vector3.One, Intensity: 1f, CastsShadows: false),
        Array.Empty<LocalLight>(),
        new SkySetup(-Vector3.UnitY, Vector3.One, Vector3.One, ExposureEv: 0f, EnvironmentMapHandle: 0));

    public static PostFxSetup PostFx { get; } = new(
        TonemapOperator.AcesFilmic,
        new BloomSetup(Enabled: false, Threshold: 1f, Intensity: 0f, MipChainLevels: 0),
        new ColourGradingSetup(Enabled: false, LutHandle: 0, Saturation: 1f, Contrast: 1f),
        AntiAliasingMode.None,
        UpscaleMode.None,
        ExposureEv: 0f);
}
