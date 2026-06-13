using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FluentAssertions;
using Opus.Editor.Core;
using Opus.Editor.Ui;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class ScaleGizmoTests
{
    [Fact]
    public void Scale_multiplies_only_the_dragged_component()
    {
        ScaleGizmo.Scale(new Float3(2f, 3f, 4f), GizmoAxis.Y, 2f).Should().Be(new Float3(2f, 6f, 4f));
    }

    [Fact]
    public void Factor_is_the_ratio_of_current_to_grab_parameter()
    {
        ScaleGizmo.TryFactor(grabParameter: 2f, currentParameter: 6f, out float factor).Should().BeTrue();
        factor.Should().Be(3f);
    }

    [Fact]
    public void A_near_zero_grab_holds_the_factor_at_one()
    {
        ScaleGizmo.TryFactor(grabParameter: 0f, currentParameter: 5f, out float factor).Should().BeFalse();
        factor.Should().Be(1f, "a grab on the gizmo origin cannot yield a stable ratio");
    }

    [Fact]
    public void Draw_list_is_three_axis_handles_each_capped_with_a_tip_cube()
    {
        var lines = new List<ViewportLine>();

        ScaleGizmo.AppendDrawLines(lines, Vector3.Zero, 2f, GizmoAxis.X);

        // 3 axis lines + 3 tip cubes (12 edges each) = 39 lines.
        lines.Should().HaveCount(39);
        lines.Count(l => l.Role == ViewportLineRole.GizmoActive).Should()
            .Be(13, "the active X axis handle and its tip cube draw with the highlight role");
    }
}
