using System.Linq;
using FluentAssertions;
using Opus.Editor.Core;
using Opus.Editor.Ui;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class EditorOutlinerTests
{
    private static readonly EditorPanelRect Panel = new(960, 32, 320, 300);

    private static EditorScene SceneWith(params string[] names)
    {
        var document = new EditorDocument("scene");
        foreach (var name in names)
        {
            document.PlaceNode(name, "m.glb", EditorTransform.Identity);
        }

        return document.Scene;
    }

    [Fact]
    public void Build_lists_one_row_per_node_with_the_selection_marked()
    {
        var scene = SceneWith("alpha", "bravo");

        var rows = EditorOutliner.Build(Panel, scene, SceneElementRef.Node(scene.Nodes[1].Id));

        rows.Should().HaveCount(2);
        rows[0].Label.Should().Contain("alpha");
        rows[1].Selected.Should().BeTrue();
        rows[0].Selected.Should().BeFalse();
    }

    [Fact]
    public void Build_highlights_every_selection_set_member_row()
    {
        var scene = SceneWith("alpha", "bravo", "charlie");

        var rows = EditorOutliner.Build(
            Panel,
            scene,
            new[] { SceneElementRef.Node(scene.Nodes[0].Id), SceneElementRef.Node(scene.Nodes[2].Id) });

        rows.Select(r => r.Selected).Should().Equal(true, false, true);
    }

    [Fact]
    public void Build_lists_lights_after_the_nodes_with_a_star_marker()
    {
        var scene = SceneWith("alpha");
        var lightId = scene.AllocateLightId();
        scene.AddLight(SceneLight.CreatePoint("lamp").WithId(lightId));

        var rows = EditorOutliner.Build(Panel, scene, SceneElementRef.Light(lightId));

        rows.Should().HaveCount(2);
        rows[1].Element.Should().Be(SceneElementRef.Light(lightId));
        rows[1].Label.Should().Be($"*{lightId.Value} lamp");
        rows[1].Selected.Should().BeTrue("the selected light's row is highlighted");
        rows[0].Selected.Should().BeFalse();
    }

    [Fact]
    public void The_renaming_row_shows_the_buffer_with_a_caret()
    {
        var scene = SceneWith("alpha", "bravo");
        var element = SceneElementRef.Node(scene.Nodes[0].Id);
        var rename = new RenameState(element, "alp");

        var rows = EditorOutliner.Build(Panel, scene, element, scrollOffset: 0, rename);

        rows[0].Label.Should().Be($"#{scene.Nodes[0].Id.Value} alp_");
        rows[1].Label.Should().Contain("bravo", "only the renaming row swaps its name for the buffer");
    }

    [Fact]
    public void Build_clips_rows_that_overflow_the_panel()
    {
        var scene = SceneWith(Enumerable.Range(0, 100).Select(i => $"n{i}").ToArray());

        var rows = EditorOutliner.Build(Panel, scene, SceneElementRef.None);

        rows.Count.Should().BeLessThan(scene.Count, "rows past the panel bottom are clipped");
        rows.Should().OnlyContain(r => r.Rect.Bottom <= Panel.Bottom);
    }

    [Fact]
    public void Hit_test_returns_the_node_under_the_pixel()
    {
        var scene = SceneWith("alpha", "bravo");
        var rows = EditorOutliner.Build(Panel, scene, SceneElementRef.None);
        var second = rows[1].Rect;

        EditorOutliner.HitTest(rows, second.X + 5, second.Y + 5)
            .Should().Be(SceneElementRef.Node(scene.Nodes[1].Id));
    }

    [Fact]
    public void Hit_test_in_the_header_above_the_rows_is_none()
    {
        var scene = SceneWith("alpha");
        var rows = EditorOutliner.Build(Panel, scene, SceneElementRef.None);

        EditorOutliner.HitTest(rows, Panel.X + 5, Panel.Y + 2).Should().Be(SceneElementRef.None);
    }

    [Fact]
    public void Visible_row_capacity_is_the_rows_that_fit_below_the_header()
    {
        EditorOutliner.VisibleRowCapacity(Panel).Should()
            .Be((Panel.Height - EditorOutliner.HeaderHeight) / EditorOutliner.RowHeight);
    }

    [Fact]
    public void Build_with_a_scroll_offset_starts_at_the_offset_row()
    {
        var scene = SceneWith(Enumerable.Range(0, 30).Select(i => $"n{i}").ToArray());

        var rows = EditorOutliner.Build(Panel, scene, SceneElementRef.None, scrollOffset: 5);

        rows[0].Element.Should().Be(
            SceneElementRef.Node(scene.Nodes[5].Id), "the list scrolled five rows down");
        rows.Should().OnlyContain(r => r.Rect.Bottom <= Panel.Bottom);
    }

    [Fact]
    public void Build_clamps_an_overscroll_so_the_last_element_stays_in_view()
    {
        var scene = SceneWith(Enumerable.Range(0, 30).Select(i => $"n{i}").ToArray());
        var lightId = scene.AllocateLightId();
        scene.AddLight(SceneLight.CreatePoint("lamp").WithId(lightId));

        var rows = EditorOutliner.Build(Panel, scene, SceneElementRef.None, scrollOffset: 999);

        rows[^1].Element.Should().Be(
            SceneElementRef.Light(lightId), "an overscroll lands on the last full page, which ends at the last light");
    }

    [Fact]
    public void Element_range_runs_forward_between_the_two_ends_inclusive()
    {
        var scene = SceneWith("alpha", "bravo", "charlie", "delta");

        var range = EditorOutliner.ElementRange(
            scene, SceneElementRef.Node(scene.Nodes[0].Id), SceneElementRef.Node(scene.Nodes[2].Id));

        range.Should().Equal(
            SceneElementRef.Node(scene.Nodes[0].Id),
            SceneElementRef.Node(scene.Nodes[1].Id),
            SceneElementRef.Node(scene.Nodes[2].Id));
    }

    [Fact]
    public void Element_range_backwards_ends_at_the_clicked_row()
    {
        var scene = SceneWith("alpha", "bravo", "charlie");

        var range = EditorOutliner.ElementRange(
            scene, SceneElementRef.Node(scene.Nodes[2].Id), SceneElementRef.Node(scene.Nodes[0].Id));

        range.Should().Equal(
            SceneElementRef.Node(scene.Nodes[2].Id),
            SceneElementRef.Node(scene.Nodes[1].Id),
            SceneElementRef.Node(scene.Nodes[0].Id));
        range[^1].Should().Be(
            SceneElementRef.Node(scene.Nodes[0].Id), "the clicked row becomes the primary");
    }

    [Fact]
    public void Element_range_spans_from_a_node_into_the_light_listing()
    {
        var scene = SceneWith("alpha", "bravo");
        var lightId = scene.AllocateLightId();
        scene.AddLight(SceneLight.CreatePoint("lamp").WithId(lightId));

        var range = EditorOutliner.ElementRange(
            scene, SceneElementRef.Node(scene.Nodes[1].Id), SceneElementRef.Light(lightId));

        range.Should().Equal(
            SceneElementRef.Node(scene.Nodes[1].Id), SceneElementRef.Light(lightId));
    }

    [Fact]
    public void Element_range_without_a_listed_anchor_degrades_to_the_clicked_row()
    {
        var scene = SceneWith("alpha", "bravo");

        var fromNone = EditorOutliner.ElementRange(
            scene, SceneElementRef.None, SceneElementRef.Node(scene.Nodes[1].Id));
        var toMissing = EditorOutliner.ElementRange(
            scene, SceneElementRef.Node(scene.Nodes[0].Id), SceneElementRef.None);

        fromNone.Should().Equal(SceneElementRef.Node(scene.Nodes[1].Id));
        toMissing.Should().BeEmpty("a range needs a valid clicked end");
    }

    [Fact]
    public void A_hidden_element_row_carries_the_localised_hidden_marker()
    {
        var scene = new EditorScene();
        scene.Add(new SceneNode(scene.AllocateId(), "terrain", null, EditorTransform.Identity).WithHidden(true));
        scene.Add(new SceneNode(scene.AllocateId(), "tank", null, EditorTransform.Identity));

        var english = EditorOutliner.Build(Panel, scene, SceneElementRef.None);
        var russian = EditorOutliner.Build(
            Panel, scene, SceneElementRef.None, strings: EditorChromeStrings.Russian);

        english[0].Label.Should().EndWith(EditorChromeStrings.English.HiddenSuffix);
        english[1].Label.Should().NotContain(EditorChromeStrings.English.HiddenSuffix);
        russian[0].Label.Should().EndWith(EditorChromeStrings.Russian.HiddenSuffix);
    }

    [Fact]
    public void Children_are_listed_under_their_parent_in_tree_order_and_indented()
    {
        var document = new EditorDocument("scene");
        var parent = document.PlaceNode("group", null, EditorTransform.Identity);
        var child = document.PlaceNode("box", null, EditorTransform.Identity);
        var sibling = document.PlaceNode("rock", null, EditorTransform.Identity);
        document.SetNodeParent(child, parent);

        var rows = EditorOutliner.Build(Panel, document.Scene, SceneElementRef.None);

        rows.Select(r => r.Element).Should().Equal(
            SceneElementRef.Node(parent),
            SceneElementRef.Node(child),
            SceneElementRef.Node(sibling));
        rows[1].Label.Should().StartWith(new string(' ', EditorOutliner.IndentSpaces), "the child row is indented");
        rows[0].Label[0].Should().NotBe(' ', "a root row is not indented");
        rows[2].Label[0].Should().NotBe(' ');
    }

    [Fact]
    public void Element_range_spans_the_tree_order_not_the_raw_document_order()
    {
        var document = new EditorDocument("scene");
        var parent = document.PlaceNode("group", null, EditorTransform.Identity);
        var child = document.PlaceNode("box", null, EditorTransform.Identity);
        var sibling = document.PlaceNode("rock", null, EditorTransform.Identity);
        document.SetNodeParent(child, parent);

        var range = EditorOutliner.ElementRange(
            document.Scene, SceneElementRef.Node(parent), SceneElementRef.Node(sibling));

        range.Should().Equal(
            SceneElementRef.Node(parent),
            SceneElementRef.Node(child),
            SceneElementRef.Node(sibling));
    }
}
