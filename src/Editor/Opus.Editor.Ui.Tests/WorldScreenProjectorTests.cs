using System.Numerics;
using FluentAssertions;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class WorldScreenProjectorTests
{
    [Fact]
    public void Camera_target_projects_to_the_screen_centre()
    {
        var camera = new OrbitCamera { Target = Vector3.Zero };
        var viewProjection = camera.ViewMatrix * camera.ProjectionMatrix(800f / 600f);

        WorldScreenProjector.TryProject(camera.Target, viewProjection, 800, 600, out var screen).Should().BeTrue();

        screen.X.Should().BeApproximately(400f, 1f);
        screen.Y.Should().BeApproximately(300f, 1f);
    }

    [Fact]
    public void A_point_behind_the_camera_does_not_project()
    {
        var camera = new OrbitCamera { Target = Vector3.Zero };
        var viewProjection = camera.ViewMatrix * camera.ProjectionMatrix(1f);
        var behind = camera.EyePosition + Vector3.Normalize(camera.EyePosition - camera.Target) * 5f;

        WorldScreenProjector.TryProject(behind, viewProjection, 800, 600, out _).Should().BeFalse();
    }
}
