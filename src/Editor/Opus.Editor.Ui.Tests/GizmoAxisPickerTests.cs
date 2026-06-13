using System.Numerics;
using FluentAssertions;
using Opus.Editor.Ui;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class GizmoAxisPickerTests
{
    private static readonly GizmoScreenHandle[] Handles =
    {
        new(GizmoAxis.X, new Vector2(100f, 100f), new Vector2(200f, 100f)),
        new(GizmoAxis.Y, new Vector2(100f, 100f), new Vector2(100f, 200f)),
    };

    [Fact]
    public void A_click_near_a_handle_picks_its_axis()
    {
        GizmoAxisPicker.Pick(new Vector2(150f, 103f), Handles).Should().Be(GizmoAxis.X);
    }

    [Fact]
    public void A_click_off_every_handle_picks_no_axis()
    {
        GizmoAxisPicker.Pick(new Vector2(150f, 150f), Handles).Should().Be(GizmoAxis.None);
    }

    [Fact]
    public void The_nearest_handle_wins_when_two_are_close()
    {
        GizmoAxisPicker.Pick(new Vector2(102f, 150f), Handles).Should().Be(GizmoAxis.Y);
    }
}
