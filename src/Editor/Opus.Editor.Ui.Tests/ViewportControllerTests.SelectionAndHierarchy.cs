using System;
using System.Numerics;
using FluentAssertions;
using Opus.Editor.Core;
using Opus.Foundation.Geometry;
using Xunit;

namespace Opus.Editor.Ui.Tests;

/// <summary>Selection and hierarchy: marquee, select-all, save-as, and the parented-node gizmo plus group / ungroup.</summary>
public sealed partial class ViewportControllerTests
{
    [Fact]
    public void A_marquee_tracks_from_begin_through_update_and_clears_on_cancel()
    {
        var controller = new ViewportController(new EditorDocument("scene"), new NullBounds());

        controller.BeginMarquee(0.2f, 0.3f);
        controller.UpdateMarquee(0.7f, 0.6f);

        controller.Marquee.Should().Be(
            new MarqueeState(new Vector2(0.2f, 0.3f), new Vector2(0.7f, 0.6f)));

        controller.CancelMarquee();

        controller.Marquee.Should().BeNull();
    }

    [Fact]
    public void Ending_a_full_viewport_marquee_selects_every_visible_element()
    {
        var document = new EditorDocument("scene");
        var node = document.PlaceNode("tank", "m.glb", EditorTransform.Identity);
        var lamp = document.AddNewPointLight(new Float3(2f, 0f, 0f));
        var hidden = document.PlaceNode("ghost", "m.glb", EditorTransform.Identity);
        document.SetNodeHidden(hidden, true);
        document.Select(SceneNodeId.None);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));

        controller.BeginMarquee(0f, 0f);
        controller.UpdateMarquee(1f, 1f);
        controller.EndMarquee(Viewport);

        document.SelectedElements.Should().Equal(
            SceneElementRef.Node(node), SceneElementRef.Light(lamp));
        controller.Marquee.Should().BeNull("the drag ended");
    }

    [Fact]
    public void Ending_a_marquee_over_empty_space_clears_the_selection()
    {
        var document = new EditorDocument("scene");
        var node = document.PlaceNode("tank", "m.glb", EditorTransform.Identity);
        document.Select(node);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));

        controller.BeginMarquee(0.95f, 0.95f);
        controller.UpdateMarquee(1f, 1f);
        controller.EndMarquee(Viewport);

        document.SelectedElements.Should().BeEmpty("a replace box around nothing deselects");
    }

    [Fact]
    public void An_additive_marquee_unions_into_the_existing_selection()
    {
        var document = new EditorDocument("scene");
        var away = document.PlaceNode(
            "away", "m.glb", EditorTransform.Identity with { Position = new Float3(100f, 0f, 0f) });
        var centre = document.PlaceNode("centre", "m.glb", EditorTransform.Identity);
        document.Select(away);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));

        controller.BeginMarquee(0.3f, 0.3f);
        controller.UpdateMarquee(0.7f, 0.7f);
        controller.EndMarquee(Viewport, additive: true);

        document.SelectedElements.Should().Equal(
            SceneElementRef.Node(away), SceneElementRef.Node(centre));
    }

    [Fact]
    public void Select_all_visible_takes_every_unhidden_node_and_light()
    {
        var document = new EditorDocument("scene");
        var node = document.PlaceNode("tank", "m.glb", EditorTransform.Identity);
        var hidden = document.PlaceNode("ghost", "m.glb", EditorTransform.Identity);
        var lamp = document.AddNewPointLight(Float3.Zero);
        document.SetNodeHidden(hidden, true);
        document.Select(SceneNodeId.None);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));

        controller.SelectAllVisible().Should().BeTrue();

        document.SelectedElements.Should().Equal(
            SceneElementRef.Node(node), SceneElementRef.Light(lamp));
    }

    [Fact]
    public void Select_all_visible_is_false_when_nothing_is_visible()
    {
        var document = new EditorDocument("scene");
        var hidden = document.PlaceNode("ghost", "m.glb", EditorTransform.Identity);
        document.SetNodeHidden(hidden, true);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));

        controller.SelectAllVisible().Should().BeFalse();

        document.SelectedElements.Should().Equal(
            new[] { SceneElementRef.Node(hidden) }, "a failed select-all leaves the selection alone");
    }

    [Fact]
    public void Begin_save_as_seeds_the_document_name_and_cancels_other_text_entry()
    {
        var document = new EditorDocument("Harbor");
        document.PlaceNode("tank", null, EditorTransform.Identity);
        var controller = new ViewportController(document, new NullBounds());
        controller.BeginRename();

        controller.BeginSaveAs();

        controller.SaveAs.Should().Be(new SaveAsState("Harbor"));
        controller.Rename.Should().BeNull("one modal at a time");
        controller.IsTextEntryActive.Should().BeTrue();
    }

    [Fact]
    public void Save_as_buffer_edits_and_commit_return_the_trimmed_name()
    {
        var controller = new ViewportController(new EditorDocument("Harbor"), new NullBounds());
        controller.BeginSaveAs();
        controller.SaveAsBackspace();
        controller.SaveAsBackspace();
        controller.SaveAsAppend('o');
        controller.SaveAsAppend('r');
        controller.SaveAsAppend(' ');

        controller.CommitSaveAs().Should().Be("Harbor");
        controller.SaveAs.Should().BeNull("the commit ends the entry");
    }

    [Fact]
    public void Committing_an_empty_save_as_buffer_returns_null()
    {
        var controller = new ViewportController(new EditorDocument("H"), new NullBounds());
        controller.BeginSaveAs();
        controller.SaveAsBackspace();

        controller.CommitSaveAs().Should().BeNull("an empty name saves nothing");
        controller.SaveAs.Should().BeNull();
    }

    [Fact]
    public void Cancelling_a_save_as_leaves_no_modal_behind()
    {
        var controller = new ViewportController(new EditorDocument("H"), new NullBounds());
        controller.BeginSaveAs();

        controller.CancelSaveAs();

        controller.SaveAs.Should().BeNull();
        controller.IsTextEntryActive.Should().BeFalse();
    }

    private static (float X01, float Y01) HandleMidpoint(ViewportController controller, Vector3 axisDirection)
    {
        float length = TranslateGizmo.HandleLength(controller.Camera.Distance);
        var origin = Project(controller, Vector3.Zero);
        var tip = Project(controller, axisDirection * length);
        return ((origin.X01 + tip.X01) / 2f, (origin.Y01 + tip.Y01) / 2f);
    }

    private static (float X01, float Y01) Project(ViewportController controller, Vector3 world)
    {
        var viewProjection = controller.Camera.ViewMatrix * controller.Camera.ProjectionMatrix(Viewport.AspectRatio);
        WorldScreenProjector.TryProject(world, viewProjection, Viewport.Width, Viewport.Height, out var pixel)
            .Should().BeTrue();
        return (pixel.X / Viewport.Width, pixel.Y / Viewport.Height);
    }

    [Fact]
    public void Gizmo_origin_for_a_parented_node_is_its_world_position()
    {
        var document = new EditorDocument("scene");
        var parent = document.PlaceNode(
            "group", null, EditorTransform.Identity with { Position = new Float3(3f, 0f, 0f) });
        var child = document.PlaceNode("box", "m.glb", EditorTransform.Identity);
        document.SetNodeParent(child, parent);
        document.Select(child);

        var origin = ElementGizmo.Origin(document, GizmoMode.Translate);

        origin.Should().NotBeNull();
        origin!.Value.X.Should().BeApproximately(3f, 1e-4f, "the gizmo sits on the node's world position");
    }

    [Fact]
    public void Dragging_a_parented_nodes_x_gizmo_moves_its_world_position_along_x()
    {
        var document = new EditorDocument("scene");
        var parent = document.PlaceNode(
            "group", null, EditorTransform.Identity with { Position = new Float3(3f, 0f, 0f) });
        var child = document.PlaceNode("box", "m.glb", EditorTransform.Identity);
        document.SetNodeParent(child, parent);
        document.Select(child);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        controller.FrameSelection();
        float length = TranslateGizmo.HandleLength(controller.Camera.Distance);
        float aspect = Viewport.AspectRatio;
        var worldOrigin = new Vector3(3f, 0f, 0f);
        var (originX01, originY01) = Project(controller, worldOrigin);
        var (tipX01, tipY01) = Project(controller, worldOrigin + new Vector3(length, 0f, 0f));

        controller.BeginGizmoDrag(GizmoAxis.X, originX01, originY01, aspect).Should().BeTrue();
        controller.UpdateGizmoDrag(tipX01, tipY01, aspect);
        controller.EndGizmoDrag();

        // The local X advanced by the world drag distance; the composed world position followed the gizmo.
        document.Scene.Find(child)!.Transform.Position.X.Should().BeApproximately(length, 5e-2f);
        SceneNodeTransforms.WorldMatrix(document.Scene, child).Translation.X
            .Should().BeApproximately(3f + length, 5e-2f, "the gizmo and the node box stayed together");
    }

    [Fact]
    public void Parent_selection_to_primary_parents_the_others_under_the_last_selected()
    {
        var document = new EditorDocument("scene");
        var child = document.PlaceNode("box", null, EditorTransform.Identity with { Position = new Float3(2f, 0f, 0f) });
        var parent = document.PlaceNode("group", null, EditorTransform.Identity with { Position = new Float3(5f, 0f, 0f) });
        var controller = new ViewportController(document, new NullBounds());
        document.SelectElements(new[] { SceneElementRef.Node(child), SceneElementRef.Node(parent) });

        controller.ParentSelectionToPrimary().Should().BeTrue();

        document.Scene.Find(child)!.ParentId.Should().Be(parent, "the non-primary node parents under the primary");
        SceneNodeTransforms.WorldMatrix(document.Scene, child).Translation.X
            .Should().BeApproximately(2f, 1e-4f, "world position preserved");
    }

    [Fact]
    public void Unparent_selection_detaches_the_selected_node()
    {
        var document = new EditorDocument("scene");
        var parent = document.PlaceNode("group", null, EditorTransform.Identity);
        var child = document.PlaceNode("box", null, EditorTransform.Identity);
        document.SetNodeParent(child, parent);
        var controller = new ViewportController(document, new NullBounds());
        document.Select(child);

        controller.UnparentSelection().Should().BeTrue();

        document.Scene.Find(child)!.ParentId.Should().BeNull();
    }

    [Fact]
    public void Group_selection_parents_the_selected_nodes_under_a_new_group()
    {
        var document = new EditorDocument("scene");
        var a = document.PlaceNode("a", null, EditorTransform.Identity);
        var b = document.PlaceNode("b", null, EditorTransform.Identity with { Position = new Float3(4f, 0f, 0f) });
        var controller = new ViewportController(document, new NullBounds());
        document.SelectElements(new[] { SceneElementRef.Node(a), SceneElementRef.Node(b) });

        controller.GroupSelection().Should().BeTrue();

        var group = document.SelectedElement;
        group.IsNode.Should().BeTrue("the new group becomes the selection");
        document.Scene.Find(a)!.ParentId.Should().Be(group.AsNode);
        document.Scene.Find(b)!.ParentId.Should().Be(group.AsNode);
    }

    [Fact]
    public void Ungroup_selection_dissolves_the_group_and_selects_its_children()
    {
        var document = new EditorDocument("scene");
        var a = document.PlaceNode("a", null, EditorTransform.Identity);
        var b = document.PlaceNode("b", null, EditorTransform.Identity with { Position = new Float3(4f, 0f, 0f) });
        var controller = new ViewportController(document, new NullBounds());
        document.SelectElements(new[] { SceneElementRef.Node(a), SceneElementRef.Node(b) });
        var group = document.GroupNodes(new[] { a, b });
        document.Select(group);

        controller.UngroupSelection().Should().BeTrue();

        document.Scene.Find(group).Should().BeNull("the empty group node is removed");
        document.Scene.Find(a)!.ParentId.Should().BeNull("the children rise to the root");
        document.SelectedElements.Should().BeEquivalentTo(
            new[] { SceneElementRef.Node(a), SceneElementRef.Node(b) });
    }

    [Fact]
    public void Ungroup_selection_is_false_with_nothing_selected()
    {
        var document = new EditorDocument("scene");
        var controller = new ViewportController(document, new NullBounds());

        controller.UngroupSelection().Should().BeFalse();
    }
}
