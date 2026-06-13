using FluentAssertions;
using Opus.Editor.Core;
using Opus.Editor.Ui;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class GizmoSnapTests
{
    [Theory]
    [InlineData(1.8f, 1f, 2f)]
    [InlineData(2.4f, 1f, 2f)]
    [InlineData(40f, 15f, 45f)]
    [InlineData(-7f, 15f, 0f)]
    public void To_step_rounds_to_the_nearest_multiple(float value, float step, float expected)
    {
        GizmoSnap.ToStep(value, step).Should().BeApproximately(expected, 1e-4f);
    }

    [Fact]
    public void Snap_axis_snaps_only_the_named_component()
    {
        GizmoSnap.SnapAxis(new Float3(1.8f, 2.7f, 3.3f), GizmoAxis.X, 1f).Should()
            .Be(new Float3(2f, 2.7f, 3.3f), "the untouched axes keep their authored values");
    }

    [Fact]
    public void Snap_scale_axis_rounds_the_magnitude_with_a_one_step_floor()
    {
        GizmoSnap.SnapScaleAxis(new Float3(1.8f, 1f, 1f), GizmoAxis.X, 0.25f).X.Should().Be(1.75f);
        GizmoSnap.SnapScaleAxis(new Float3(0.05f, 1f, 1f), GizmoAxis.X, 0.25f).X.Should()
            .Be(0.25f, "the magnitude never snaps below one step, so the node never collapses");
    }

    [Fact]
    public void Snap_scale_axis_preserves_a_mirror_sign()
    {
        GizmoSnap.SnapScaleAxis(new Float3(-1.1f, 1f, 1f), GizmoAxis.X, 0.25f).X.Should()
            .Be(-1f, "a mirrored axis stays mirrored after snapping");
    }
}
