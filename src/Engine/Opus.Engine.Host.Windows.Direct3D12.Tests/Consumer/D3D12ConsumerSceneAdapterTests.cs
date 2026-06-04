using System;
using System.Numerics;
using FluentAssertions;
using Opus.Engine.Consumer.Scene;
using Opus.Engine.Host.Windows.Direct3D12.Consumer;
using Opus.Engine.Renderer;
using Xunit;

namespace Opus.Engine.Host.Windows.Direct3D12.Tests.Consumer;

public sealed class D3D12ConsumerSceneAdapterTests
{
    [Fact]
    public void To_frame_camera_set_preserves_main_and_auxiliary()
    {
        var main = BuildCamera(originX: 10);
        var aux = BuildCamera(originX: -10);
        var consumer = new ConsumerCameraSet(main, new[] { aux });

        var translated = D3D12ConsumerSceneAdapter.ToFrameCameraSet(consumer);

        translated.Main.PositionWorld.X.Should().Be(10);
        translated.Auxiliary.Should().HaveCount(1);
        translated.Auxiliary[0].PositionWorld.X.Should().Be(-10);
    }

    [Fact]
    public void To_frame_camera_set_with_single_main_has_empty_auxiliary()
    {
        var consumer = ConsumerCameraSet.SingleMain(BuildCamera(originX: 0));

        var translated = D3D12ConsumerSceneAdapter.ToFrameCameraSet(consumer);

        translated.Auxiliary.Should().BeEmpty();
    }

    [Fact]
    public void To_lighting_setup_translates_sun_local_lights_and_sky()
    {
        var sun = new ConsumerDirectionalLight(
            Vector3.Normalize(new Vector3(-0.3f, -1f, -0.2f)),
            new Vector3(0.9f, 0.95f, 1f),
            Intensity: 2f,
            CastsShadows: true);
        var local = new ConsumerLocalLight(
            ConsumerLocalLightKind.Spot,
            PositionWorld: new Vector3(0, 5, 0),
            DirectionWorld: -Vector3.UnitY,
            Colour: Vector3.One,
            Intensity: 5f,
            Range: 12f,
            SpotInnerAngleRadians: 0.4f,
            SpotOuterAngleRadians: 0.8f,
            CastsShadows: false);
        var sky = new ConsumerSkySnapshot(
            SunDirectionWorld: sun.DirectionWorld,
            ZenithColour: new Vector3(0.4f, 0.5f, 0.6f),
            HorizonColour: new Vector3(0.1f, 0.15f, 0.2f),
            ExposureEv: -0.5f,
            EnvironmentMapHandle: 17);
        var lighting = new ConsumerLightingSnapshot(sun, new[] { local }, sky);

        var translated = D3D12ConsumerSceneAdapter.ToLightingSetup(lighting);

        translated.Sun.CastsShadows.Should().BeTrue();
        translated.Sun.Intensity.Should().Be(2f);
        translated.LocalLights.Should().ContainSingle();
        translated.LocalLights[0].Kind.Should().Be(LocalLightKind.Spot);
        translated.Sky.EnvironmentMapHandle.Should().Be(17);
        translated.Sky.ExposureEv.Should().Be(-0.5f);
    }

    [Theory]
    [InlineData(ConsumerLocalLightKind.Point, LocalLightKind.Point)]
    [InlineData(ConsumerLocalLightKind.Spot, LocalLightKind.Spot)]
    [InlineData(ConsumerLocalLightKind.Area, LocalLightKind.Area)]
    public void Local_light_kind_maps_one_to_one(ConsumerLocalLightKind input, LocalLightKind expected)
    {
        var lighting = BuildLightingWithLocalKind(input);

        var translated = D3D12ConsumerSceneAdapter.ToLightingSetup(lighting);

        translated.LocalLights[0].Kind.Should().Be(expected);
    }

    [Fact]
    public void Unknown_local_light_kind_throws_at_boundary()
    {
        var lighting = BuildLightingWithLocalKind((ConsumerLocalLightKind)99);

        Action act = () => D3D12ConsumerSceneAdapter.ToLightingSetup(lighting);

        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*Unknown consumer local-light kind*");
    }

    [Fact]
    public void Lighting_setup_rejects_null_input()
    {
        Action act = () => D3D12ConsumerSceneAdapter.ToLightingSetup(lighting: null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Camera_set_rejects_null_input()
    {
        Action act = () => D3D12ConsumerSceneAdapter.ToFrameCameraSet(cameras: null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static ConsumerCamera BuildCamera(float originX)
    {
        var position = new Vector3(originX, 4f, 8f);
        return new ConsumerCamera(
            Matrix4x4.CreateLookAt(position, Vector3.Zero, Vector3.UnitY),
            Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3f, 16f / 9f, 0.1f, 500f),
            position,
            Vector3.Normalize(-position),
            NearPlane: 0.1f,
            FarPlane: 500f,
            FovYRadians: MathF.PI / 3f,
            AspectRatio: 16f / 9f);
    }

    private static ConsumerLightingSnapshot BuildLightingWithLocalKind(ConsumerLocalLightKind kind)
    {
        var sun = new ConsumerDirectionalLight(
            -Vector3.UnitY,
            Vector3.One,
            Intensity: 1f,
            CastsShadows: false);
        var local = new ConsumerLocalLight(
            kind,
            PositionWorld: Vector3.Zero,
            DirectionWorld: -Vector3.UnitY,
            Colour: Vector3.One,
            Intensity: 1f,
            Range: 1f,
            SpotInnerAngleRadians: 0f,
            SpotOuterAngleRadians: 0f,
            CastsShadows: false);
        var sky = new ConsumerSkySnapshot(
            -Vector3.UnitY,
            Vector3.Zero,
            Vector3.Zero,
            ExposureEv: 0f,
            EnvironmentMapHandle: 0);
        return new ConsumerLightingSnapshot(sun, new[] { local }, sky);
    }
}
