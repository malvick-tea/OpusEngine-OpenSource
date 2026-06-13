using System;
using System.Numerics;
using FluentAssertions;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class OrbitCameraTests
{
    [Fact]
    public void Eye_is_distance_away_from_target()
    {
        var camera = new OrbitCamera { Target = Vector3.Zero };

        Vector3.Distance(camera.EyePosition, camera.Target).Should().BeApproximately(camera.Distance, 1e-3f);
    }

    [Fact]
    public void Default_eye_sits_above_the_target()
    {
        var camera = new OrbitCamera { Target = Vector3.Zero };

        camera.EyePosition.Y.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void Orbit_clamps_pitch_to_the_limits()
    {
        var camera = new OrbitCamera();

        camera.Orbit(0f, 1000f);
        camera.PitchDegrees.Should().Be(OrbitCamera.MaxPitchDegrees);

        camera.Orbit(0f, -10000f);
        camera.PitchDegrees.Should().Be(OrbitCamera.MinPitchDegrees);
    }

    [Fact]
    public void Orbit_wraps_yaw_into_zero_to_360()
    {
        var camera = new OrbitCamera();

        camera.Orbit(360f, 0f);

        camera.YawDegrees.Should().BeApproximately(45f, 1e-3f);
    }

    [Fact]
    public void Distance_clamps_to_the_minimum()
    {
        var camera = new OrbitCamera();

        camera.SetDistance(0.0001f);
        camera.Distance.Should().Be(OrbitCamera.MinDistance);

        camera.SetDistance(50f);
        camera.Zoom(0.00001f);
        camera.Distance.Should().Be(OrbitCamera.MinDistance);
    }

    [Fact]
    public void Zoom_rejects_a_non_positive_factor()
    {
        var camera = new OrbitCamera();

        var act = () => camera.Zoom(0f);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Centre_pick_ray_points_from_the_eye_toward_the_target()
    {
        var camera = new OrbitCamera { Target = Vector3.Zero };

        var ray = camera.PickRay(0.5f, 0.5f, 1.0f);

        ray.Origin.Should().Be(camera.EyePosition);
        var expected = Vector3.Normalize(camera.Target - camera.EyePosition);
        Vector3.Dot(ray.Direction, expected).Should().BeApproximately(1f, 1e-4f);
    }

    [Fact]
    public void Pan_moves_the_target()
    {
        var camera = new OrbitCamera { Target = Vector3.Zero };

        camera.Pan(1f, 0f);

        camera.Target.Should().NotBe(Vector3.Zero);
    }

    [Fact]
    public void Reset_restores_the_home_view_after_any_navigation()
    {
        var camera = new OrbitCamera();
        camera.Orbit(120f, -40f);
        camera.Pan(3f, -2f);
        camera.Zoom(0.25f);

        camera.Reset();

        camera.Target.Should().Be(Vector3.Zero);
        camera.Distance.Should().Be(OrbitCamera.DefaultDistance);
        camera.YawDegrees.Should().Be(OrbitCamera.DefaultYawDegrees);
        camera.PitchDegrees.Should().Be(OrbitCamera.DefaultPitchDegrees);
    }
}
