using System.Numerics;
using FluentAssertions;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class EditorViewportProjectionTests
{
    [Fact]
    public void The_camera_target_projects_to_the_centre_of_the_viewport_rect()
    {
        var camera = new OrbitCamera { Target = Vector3.Zero };
        var viewport = new EditorPanelRect(100, 50, 800, 600);
        var lines = new[] { new ViewportLine(camera.Target, camera.Target, ViewportLineRole.GridAxis) };

        var projected = EditorViewportProjection.Project(camera, viewport, lines);

        projected.Should().HaveCount(1);
        projected[0].A.X.Should().BeApproximately(viewport.X + (viewport.Width / 2f), 1f);
        projected[0].A.Y.Should().BeApproximately(viewport.Y + (viewport.Height / 2f), 1f);
        projected[0].Role.Should().Be(ViewportLineRole.GridAxis);
    }

    [Fact]
    public void A_line_with_an_endpoint_behind_the_camera_is_dropped()
    {
        var camera = new OrbitCamera { Target = Vector3.Zero };
        var viewport = new EditorPanelRect(0, 0, 800, 600);
        var behind = camera.EyePosition + (Vector3.Normalize(camera.EyePosition - camera.Target) * 5f);
        var lines = new[] { new ViewportLine(camera.Target, behind, ViewportLineRole.NodeBounds) };

        var projected = EditorViewportProjection.Project(camera, viewport, lines);

        projected.Should().BeEmpty();
    }

    [Fact]
    public void Projected_points_are_offset_into_a_non_origin_viewport_rect()
    {
        var camera = new OrbitCamera { Target = Vector3.Zero };
        var origin = new EditorPanelRect(0, 0, 640, 480);
        var shifted = new EditorPanelRect(200, 120, 640, 480);
        var lines = new[] { new ViewportLine(camera.Target, camera.Target, ViewportLineRole.Selection) };

        var atOrigin = EditorViewportProjection.Project(camera, origin, lines);
        var atShifted = EditorViewportProjection.Project(camera, shifted, lines);

        (atShifted[0].A - atOrigin[0].A).Should().Be(new Vector2(shifted.X, shifted.Y));
    }
}
