using System.Linq;
using FluentAssertions;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class ViewportGnomonTests
{
    private static readonly EditorPanelRect Viewport = new(0, 32, 960, 600);

    [Fact]
    public void Three_arms_share_the_corner_anchor_and_carry_the_axis_roles()
    {
        var arms = ViewportGnomon.Build(new OrbitCamera(), Viewport);

        arms.Should().HaveCount(3);
        arms.Select(a => a.Role).Should().Equal(
            ViewportLineRole.GizmoX, ViewportLineRole.GizmoY, ViewportLineRole.GizmoZ);
        arms.Select(a => a.A).Distinct().Should().HaveCount(1, "every arm starts at the corner anchor");
        var anchor = arms[0].A;
        anchor.X.Should().Be(Viewport.X + ViewportGnomon.CornerInsetPixels);
        anchor.Y.Should().Be(Viewport.Bottom - ViewportGnomon.CornerInsetPixels);
    }

    [Fact]
    public void The_world_up_arm_points_up_on_screen()
    {
        var arms = ViewportGnomon.Build(new OrbitCamera(), Viewport);

        var up = arms.Single(a => a.Role == ViewportLineRole.GizmoY);
        up.B.Y.Should().BeLessThan(up.A.Y, "world +Y projects upward (screen Y runs down)");
    }

    [Fact]
    public void Orbiting_half_a_turn_flips_the_x_arm()
    {
        var camera = new OrbitCamera();
        float before = ViewportGnomon.Build(camera, Viewport)
            .Single(a => a.Role == ViewportLineRole.GizmoX).B.X;

        camera.Orbit(180f, 0f);
        float after = ViewportGnomon.Build(camera, Viewport)
            .Single(a => a.Role == ViewportLineRole.GizmoX).B.X;

        var anchorX = Viewport.X + ViewportGnomon.CornerInsetPixels;
        (before - anchorX).Should().NotBe(0f);
        (after - anchorX).Should().BeApproximately(-(before - anchorX), 1e-3f, "the arm mirrors with the view");
    }

    [Fact]
    public void Arms_never_exceed_the_full_length()
    {
        var camera = new OrbitCamera();
        camera.Orbit(33f, 21f);

        foreach (var arm in ViewportGnomon.Build(camera, Viewport))
        {
            (arm.B - arm.A).Length().Should().BeLessThanOrEqualTo(
                ViewportGnomon.ArmLengthPixels + 1e-3f, "foreshortening only ever shortens an arm");
        }
    }
}
