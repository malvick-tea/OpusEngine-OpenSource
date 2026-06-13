using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FluentAssertions;
using Opus.Editor.Core;
using Opus.Editor.Ui;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class RotateGizmoTests
{
    [Fact]
    public void Rotate_advances_only_the_dragged_component()
    {
        RotateGizmo.Rotate(new Float3(10f, 20f, 30f), GizmoAxis.Y, 45f).Should().Be(new Float3(10f, 65f, 30f));
    }

    [Fact]
    public void Try_angle_measures_the_in_plane_angle_of_the_pick_ray()
    {
        // Looking straight down at the Y ring (the XZ plane): a ray over the +X point reads a quarter turn,
        // a ray over the +Z point reads zero (the +Z direction is the ring's angle origin).
        var overX = new Ray(new Vector3(1f, 5f, 0f), -Vector3.UnitY);
        var overZ = new Ray(new Vector3(0f, 5f, 1f), -Vector3.UnitY);

        RotateGizmo.TryAngle(overX, Vector3.Zero, GizmoAxis.Y, out float atX).Should().BeTrue();
        RotateGizmo.TryAngle(overZ, Vector3.Zero, GizmoAxis.Y, out float atZ).Should().BeTrue();

        atX.Should().BeApproximately(MathF.PI / 2f, 1e-4f);
        atZ.Should().BeApproximately(0f, 1e-4f);
    }

    [Fact]
    public void Try_angle_is_false_when_the_ray_is_parallel_to_the_rotation_plane()
    {
        var edgeOn = new Ray(new Vector3(0f, 0f, 5f), -Vector3.UnitZ);

        RotateGizmo.TryAngle(edgeOn, Vector3.Zero, GizmoAxis.Y, out _).Should()
            .BeFalse("a ray in the rotation plane cannot resolve a stable ring angle");
    }

    [Fact]
    public void Try_angle_is_false_when_the_plane_is_behind_the_camera()
    {
        var awayFromPlane = new Ray(new Vector3(0f, 5f, 0f), Vector3.UnitY);

        RotateGizmo.TryAngle(awayFromPlane, Vector3.Zero, GizmoAxis.Y, out _).Should().BeFalse();
    }

    [Fact]
    public void Delta_degrees_is_the_signed_sweep_from_grab_to_current()
    {
        RotateGizmo.DeltaDegrees(grabAngleRadians: 0f, currentAngleRadians: MathF.PI / 2f).Should()
            .BeApproximately(90f, 1e-3f);
    }

    [Theory]
    [InlineData(45f, 45f)]
    [InlineData(180f, 180f)]
    [InlineData(190f, -170f)]
    [InlineData(-190f, 170f)]
    [InlineData(360f, 0f)]
    public void Wrap_signed_degrees_folds_into_the_half_open_turn(float input, float expected)
    {
        RotateGizmo.WrapSignedDegrees(input).Should().BeApproximately(expected, 1e-3f);
    }

    [Fact]
    public void Draw_list_is_three_rings_with_the_active_axis_highlighted()
    {
        var lines = new List<ViewportLine>();

        RotateGizmo.AppendDrawLines(lines, Vector3.Zero, 2f, GizmoAxis.X);

        lines.Should().HaveCount(3 * RotateGizmo.RingSegmentCount);
        lines.Count(l => l.Role == ViewportLineRole.GizmoActive).Should()
            .Be(RotateGizmo.RingSegmentCount, "the active X ring draws with the highlight role");
        lines.Should().OnlyContain(
            l => MathF.Abs(Vector3.Distance(l.A, Vector3.Zero) - 2f) < 1e-3f,
            "every ring point sits one radius from the gizmo origin");
    }
}
