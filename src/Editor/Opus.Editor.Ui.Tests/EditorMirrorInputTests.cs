using System.Numerics;
using FluentAssertions;
using Opus.Editor.Core;
using Opus.Foundation.Geometry;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class EditorMirrorInputTests
{
    private static readonly EditorPanelRect DslPanel = new(960, 400, 320, 120);
    private static readonly Aabb UnitBox = new(new Vector3(-1f), new Vector3(1f));

    [Fact]
    public void Wheel_down_over_the_mirror_scrolls_toward_the_bottom()
    {
        var controller = ControllerWith(30);
        var input = new FakeInputSource { MousePosition = (DslPanel.X + 5, DslPanel.Y + 50), MouseWheelDelta = -1f };

        EditorMirrorInput.Apply(input, controller, DslPanel);

        controller.MirrorScroll.Should().Be(1);
    }

    [Fact]
    public void Wheel_up_at_the_top_is_clamped_to_zero()
    {
        var controller = ControllerWith(30);
        var input = new FakeInputSource { MousePosition = (DslPanel.X + 5, DslPanel.Y + 50), MouseWheelDelta = 1f };

        EditorMirrorInput.Apply(input, controller, DslPanel);

        controller.MirrorScroll.Should().Be(0, "the mirror is already at the top");
    }

    [Fact]
    public void Wheel_outside_the_mirror_panel_does_not_scroll_it()
    {
        var controller = ControllerWith(30);
        var input = new FakeInputSource { MousePosition = (100, 400), MouseWheelDelta = -1f };

        EditorMirrorInput.Apply(input, controller, DslPanel);

        controller.MirrorScroll.Should().Be(0);
    }

    [Fact]
    public void Wheel_down_never_scrolls_past_the_last_full_page()
    {
        var controller = ControllerWith(30);
        controller.SetMirrorScroll(10_000);
        var input = new FakeInputSource { MousePosition = (DslPanel.X + 5, DslPanel.Y + 50), MouseWheelDelta = -1f };

        EditorMirrorInput.Apply(input, controller, DslPanel);

        int capacity = EditorFrameDrawer.MirrorLineCapacity(DslPanel);
        controller.MirrorScroll.Should().Be(controller.MirrorLineCount - capacity, "an overscroll lands on the last full page");
    }

    [Fact]
    public void A_mirror_shorter_than_the_panel_does_not_scroll()
    {
        var controller = ControllerWith(0);
        var input = new FakeInputSource { MousePosition = (DslPanel.X + 5, DslPanel.Y + 50), MouseWheelDelta = -1f };

        EditorMirrorInput.Apply(input, controller, DslPanel);

        controller.MirrorScroll.Should().Be(0, "an empty scene's mirror fits the panel whole");
    }

    private static ViewportController ControllerWith(int nodeCount)
    {
        var document = new EditorDocument("scene");
        for (int i = 0; i < nodeCount; i++)
        {
            document.PlaceNode($"n{i}", "m.glb", EditorTransform.Identity);
        }

        return new ViewportController(document, new FixedBounds(UnitBox));
    }
}
