using FluentAssertions;
using Opus.Editor.Core;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class LightAimTests
{
    private const float Tolerance = 1e-5f;

    [Fact]
    public void Rotating_a_down_aim_ninety_degrees_about_x_points_it_along_z()
    {
        var rotated = LightAim.Rotate(new Float3(0f, -1f, 0f), GizmoAxis.X, 90f);

        rotated.X.Should().BeApproximately(0f, Tolerance);
        rotated.Y.Should().BeApproximately(0f, Tolerance);
        rotated.Z.Should().BeApproximately(-1f, Tolerance);
    }

    [Fact]
    public void Rotating_about_the_aim_axis_itself_changes_nothing()
    {
        var rotated = LightAim.Rotate(new Float3(0f, -1f, 0f), GizmoAxis.Y, 45f);

        rotated.X.Should().BeApproximately(0f, Tolerance);
        rotated.Y.Should().BeApproximately(-1f, Tolerance);
        rotated.Z.Should().BeApproximately(0f, Tolerance);
    }

    [Fact]
    public void A_zero_direction_is_returned_unchanged()
    {
        LightAim.Rotate(Float3.Zero, GizmoAxis.X, 90f).Should().Be(Float3.Zero);
    }

    [Fact]
    public void Rotation_preserves_the_direction_length()
    {
        var rotated = LightAim.Rotate(new Float3(0f, -2f, 0f), GizmoAxis.Z, 30f).ToVector3();

        rotated.Length().Should().BeApproximately(2f, Tolerance);
    }
}
