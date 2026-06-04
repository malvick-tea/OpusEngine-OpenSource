using System;
using System.Numerics;
using FluentAssertions;
using Opus.Engine.Ui.Text;
using Xunit;

namespace Opus.Engine.Ui.Direct3D12.Tests.Text;

/// <summary>Pure projection behaviour for world-space labels (M5.6). A camera at (0,0,5) looks down
/// -Z at the origin with +Y up over a 16:9 viewport, so the look-at target lands dead-centre and the
/// invariants (centre / up / behind-camera / off-frustum) hold without depending on exact fov pixels.</summary>
public sealed class WorldSpaceTextProjectorTests
{
    private const int ViewportWidth = 1920;
    private const int ViewportHeight = 1080;

    private static Matrix4x4 ViewProjection()
    {
        var view = Matrix4x4.CreateLookAt(new Vector3(0f, 0f, 5f), Vector3.Zero, Vector3.UnitY);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(
            MathF.PI / 3f, ViewportWidth / (float)ViewportHeight, nearPlaneDistance: 0.1f, farPlaneDistance: 100f);
        return view * projection;
    }

    [Fact]
    public void Look_at_target_projects_to_the_viewport_centre()
    {
        var anchor = WorldSpaceTextProjector.Project(Vector3.Zero, ViewProjection(), ViewportWidth, ViewportHeight);

        anchor.Visible.Should().BeTrue();
        anchor.ScreenX.Should().BeApproximately(ViewportWidth / 2f, 0.5f);
        anchor.ScreenY.Should().BeApproximately(ViewportHeight / 2f, 0.5f);
        anchor.NormalizedDepth.Should().BeInRange(0f, 1f);
    }

    [Fact]
    public void A_point_above_the_target_projects_higher_on_screen()
    {
        var anchor = WorldSpaceTextProjector.Project(new Vector3(0f, 0.5f, 0f), ViewProjection(), ViewportWidth, ViewportHeight);

        anchor.Visible.Should().BeTrue();
        anchor.ScreenY.Should().BeLessThan(ViewportHeight / 2f, "world +Y maps to a smaller screen y (y grows downward)");
    }

    [Fact]
    public void A_point_behind_the_camera_is_not_visible()
    {
        var anchor = WorldSpaceTextProjector.Project(new Vector3(0f, 0f, 10f), ViewProjection(), ViewportWidth, ViewportHeight);

        anchor.Visible.Should().BeFalse();
    }

    [Fact]
    public void A_point_far_off_to_the_side_is_culled()
    {
        var anchor = WorldSpaceTextProjector.Project(new Vector3(1000f, 0f, 0f), ViewProjection(), ViewportWidth, ViewportHeight);

        anchor.Visible.Should().BeFalse();
    }

    [Fact]
    public void Hidden_anchor_carries_no_screen_position()
    {
        WorldSpaceTextAnchor.Hidden.Visible.Should().BeFalse();
        WorldSpaceTextAnchor.Hidden.PixelX.Should().Be(0);
        WorldSpaceTextAnchor.Hidden.PixelY.Should().Be(0);
    }

    [Fact]
    public void Centered_left_offsets_a_measured_label_by_half_its_width()
    {
        var anchor = new WorldSpaceTextAnchor(true, ScreenX: 200f, ScreenY: 100f, NormalizedDepth: 0.5f);

        anchor.CenteredLeft(measuredWidth: 80).Should().Be(160);
    }

    [Fact]
    public void Zero_viewport_is_a_named_boundary_failure()
    {
        var act = () => WorldSpaceTextProjector.Project(Vector3.Zero, ViewProjection(), viewportWidth: 0, viewportHeight: 1080);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
