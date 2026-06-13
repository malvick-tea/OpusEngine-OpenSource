using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FluentAssertions;
using Opus.Editor.Core;
using Opus.Editor.Ui;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class TranslateGizmoTests
{
    [Fact]
    public void Handles_run_one_length_along_each_positive_axis()
    {
        var handles = TranslateGizmo.Handles(Vector3.Zero, 2f);

        handles.Should().HaveCount(3);
        handles.Single(h => h.Axis == GizmoAxis.X).End.Should().Be(new Vector3(2f, 0f, 0f));
        handles.Single(h => h.Axis == GizmoAxis.Y).End.Should().Be(new Vector3(0f, 2f, 0f));
        handles.Single(h => h.Axis == GizmoAxis.Z).End.Should().Be(new Vector3(0f, 0f, 2f));
    }

    [Fact]
    public void Translate_shifts_only_the_dragged_component()
    {
        TranslateGizmo.Translate(new Float3(1f, 2f, 3f), GizmoAxis.Y, 5f).Should().Be(new Float3(1f, 7f, 3f));
    }

    [Fact]
    public void Handle_length_scales_with_camera_distance()
    {
        TranslateGizmo.HandleLength(10f).Should().BeApproximately(10f * TranslateGizmo.HandleLengthFactor, 1e-4f);
    }

    [Fact]
    public void The_active_axis_draws_with_the_highlight_role()
    {
        var lines = new List<ViewportLine>();

        TranslateGizmo.AppendDrawLines(lines, Vector3.Zero, 2f, GizmoAxis.Y);

        lines.Select(l => l.Role).Should().BeEquivalentTo(
            new[] { ViewportLineRole.GizmoX, ViewportLineRole.GizmoActive, ViewportLineRole.GizmoZ });
    }
}
