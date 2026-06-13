using FluentAssertions;
using Xunit;

namespace Opus.Editor.Core.Tests;

/// <summary>The selection set (toggle / replace / clamp) and the multi-element group operations (remove / hide / duplicate) plus the remaining light / visibility / document-rename edits.</summary>
public sealed partial class EditorDocumentTests
{
    [Fact]
    public void A_plain_selection_is_a_one_entry_set()
    {
        var document = new EditorDocument("scene");
        var node = document.PlaceNode("a", null, EditorTransform.Identity);

        document.SelectedElements.Should().Equal(SceneElementRef.Node(node));
    }

    [Fact]
    public void Toggle_select_adds_a_second_element_as_the_new_primary()
    {
        var document = new EditorDocument("scene");
        var first = document.PlaceNode("a", null, EditorTransform.Identity);
        var second = document.PlaceNode("b", null, EditorTransform.Identity);
        document.Select(first);

        document.ToggleSelect(SceneElementRef.Node(second));

        document.SelectedElements.Should().Equal(SceneElementRef.Node(first), SceneElementRef.Node(second));
        document.SelectedElement.Should().Be(SceneElementRef.Node(second), "the newest member is the primary");
        document.IsDirty.Should().BeTrue("only because of the placements — selection itself never dirties");
    }

    [Fact]
    public void Toggle_select_removes_an_already_selected_member_and_the_primary_falls_back()
    {
        var document = new EditorDocument("scene");
        var first = document.PlaceNode("a", null, EditorTransform.Identity);
        var second = document.PlaceNode("b", null, EditorTransform.Identity);
        document.Select(first);
        document.ToggleSelect(SceneElementRef.Node(second));

        document.ToggleSelect(SceneElementRef.Node(second));

        document.SelectedElements.Should().Equal(SceneElementRef.Node(first));
        document.SelectedElement.Should().Be(SceneElementRef.Node(first));
    }

    [Fact]
    public void Toggle_select_mixes_nodes_and_lights_in_one_set()
    {
        var document = new EditorDocument("scene");
        var node = document.PlaceNode("a", null, EditorTransform.Identity);
        var lamp = document.AddNewPointLight(Float3.Zero);
        document.Select(node);

        document.ToggleSelect(SceneElementRef.Light(lamp));

        document.SelectedElements.Should().Equal(SceneElementRef.Node(node), SceneElementRef.Light(lamp));
        document.Selection.Should().Be(SceneNodeId.None, "the primary is the light, so the node view is empty");
        document.LightSelection.Should().Be(lamp);
    }

    [Fact]
    public void Toggle_select_ignores_missing_elements_and_invalid_refs()
    {
        var document = new EditorDocument("scene");
        var node = document.PlaceNode("a", null, EditorTransform.Identity);

        document.ToggleSelect(SceneElementRef.None);
        document.ToggleSelect(SceneElementRef.Node(new SceneNodeId(99)));

        document.SelectedElements.Should().Equal(SceneElementRef.Node(node));
    }

    [Fact]
    public void Select_elements_replaces_the_whole_set_in_the_given_order()
    {
        var document = new EditorDocument("scene");
        var first = document.PlaceNode("a", null, EditorTransform.Identity);
        var second = document.PlaceNode("b", null, EditorTransform.Identity);
        var lamp = document.AddNewPointLight(Float3.Zero);
        document.Select(second);
        document.MarkSaved();

        document.SelectElements(new[] { SceneElementRef.Node(first), SceneElementRef.Light(lamp) });

        document.SelectedElements.Should().Equal(SceneElementRef.Node(first), SceneElementRef.Light(lamp));
        document.SelectedElement.Should().Be(SceneElementRef.Light(lamp), "the last element is the primary");
        document.IsDirty.Should().BeFalse("selection is UI state and never dirties the document");
    }

    [Fact]
    public void Select_elements_with_an_empty_list_clears_the_selection()
    {
        var document = new EditorDocument("scene");
        document.PlaceNode("a", null, EditorTransform.Identity);

        document.SelectElements(System.Array.Empty<SceneElementRef>());

        document.SelectedElements.Should().BeEmpty("boxing empty space deselects everything");
    }

    [Fact]
    public void Select_elements_additive_unions_without_duplicating_members()
    {
        var document = new EditorDocument("scene");
        var first = document.PlaceNode("a", null, EditorTransform.Identity);
        var second = document.PlaceNode("b", null, EditorTransform.Identity);
        document.Select(first);

        document.SelectElements(
            new[] { SceneElementRef.Node(first), SceneElementRef.Node(second) }, additive: true);

        document.SelectedElements.Should().Equal(
            SceneElementRef.Node(first), SceneElementRef.Node(second));
    }

    [Fact]
    public void Select_elements_drops_invalid_refs_and_missing_elements()
    {
        var document = new EditorDocument("scene");
        var node = document.PlaceNode("a", null, EditorTransform.Identity);

        document.SelectElements(new[]
        {
            SceneElementRef.None,
            SceneElementRef.Node(new SceneNodeId(99)),
            SceneElementRef.Node(node),
            SceneElementRef.Node(node),
        });

        document.SelectedElements.Should().Equal(SceneElementRef.Node(node));
    }

    [Fact]
    public void A_plain_select_collapses_the_set_to_that_element()
    {
        var document = new EditorDocument("scene");
        var first = document.PlaceNode("a", null, EditorTransform.Identity);
        var second = document.PlaceNode("b", null, EditorTransform.Identity);
        document.Select(first);
        document.ToggleSelect(SceneElementRef.Node(second));

        document.Select(second);

        document.SelectedElements.Should().Equal(SceneElementRef.Node(second));
    }

    [Fact]
    public void Removing_a_set_member_drops_only_it_from_the_set()
    {
        var document = new EditorDocument("scene");
        var first = document.PlaceNode("a", null, EditorTransform.Identity);
        var second = document.PlaceNode("b", null, EditorTransform.Identity);
        document.Select(first);
        document.ToggleSelect(SceneElementRef.Node(second));

        document.RemoveNode(second).Should().BeTrue();

        document.SelectedElements.Should().Equal(SceneElementRef.Node(first));
    }

    [Fact]
    public void Undo_drops_dead_members_from_the_selection_set()
    {
        var document = new EditorDocument("scene");
        var first = document.PlaceNode("a", null, EditorTransform.Identity);
        document.PlaceNode("b", null, EditorTransform.Identity);
        document.ToggleSelect(SceneElementRef.Node(first));

        document.Undo().Should().BeTrue("undo the second placement");

        document.SelectedElements.Should().Equal(
            new[] { SceneElementRef.Node(first) }, "the undone node left the set, the survivor stays");
    }

    [Fact]
    public void Remove_elements_deletes_nodes_and_lights_as_one_undoable_edit()
    {
        var document = new EditorDocument("scene");
        var node = document.PlaceNode("a", null, EditorTransform.Identity);
        var lamp = document.AddNewPointLight(Float3.Zero);
        int depth = document.Commands.UndoDepth;

        document.RemoveElements(new[] { SceneElementRef.Node(node), SceneElementRef.Light(lamp) })
            .Should().BeTrue();

        document.Scene.Count.Should().Be(0);
        document.Scene.LightCount.Should().Be(0);
        document.SelectedElements.Should().BeEmpty("the removed members left the selection set");
        document.Commands.UndoDepth.Should().Be(depth + 1, "the group delete is one history entry");

        document.Undo().Should().BeTrue();
        document.Scene.Count.Should().Be(1);
        document.Scene.LightCount.Should().Be(1, "one undo restored the whole group");
    }

    [Fact]
    public void Remove_elements_skips_missing_refs_and_is_false_when_nothing_was_removed()
    {
        var document = new EditorDocument("scene");
        var node = document.PlaceNode("a", null, EditorTransform.Identity);

        document.RemoveElements(new[] { SceneElementRef.Node(new SceneNodeId(99)) }).Should().BeFalse();
        document.RemoveElements(new[] { SceneElementRef.Node(node), SceneElementRef.Node(new SceneNodeId(99)) })
            .Should().BeTrue();

        document.Scene.Count.Should().Be(0);
    }

    [Fact]
    public void Set_elements_hidden_changes_only_the_elements_not_already_in_that_state()
    {
        var document = new EditorDocument("scene");
        var visible = document.PlaceNode("a", null, EditorTransform.Identity);
        var hidden = document.PlaceNode("b", null, EditorTransform.Identity);
        document.SetNodeHidden(hidden, true);
        int depth = document.Commands.UndoDepth;

        document.SetElementsHidden(
            new[] { SceneElementRef.Node(visible), SceneElementRef.Node(hidden) }, hidden: true).Should().BeTrue();

        document.Scene.Find(visible)!.Hidden.Should().BeTrue();
        document.Scene.Find(hidden)!.Hidden.Should().BeTrue();
        document.Commands.UndoDepth.Should().Be(depth + 1);

        document.Undo().Should().BeTrue();
        document.Scene.Find(visible)!.Hidden.Should().BeFalse("the undo restored the changed element");
        document.Scene.Find(hidden)!.Hidden.Should().BeTrue("the already-hidden element never changed");
    }

    [Fact]
    public void Set_elements_hidden_is_false_when_every_element_already_matches()
    {
        var document = new EditorDocument("scene");
        var node = document.PlaceNode("a", null, EditorTransform.Identity);

        document.SetElementsHidden(new[] { SceneElementRef.Node(node) }, hidden: false).Should().BeFalse();
    }

    [Fact]
    public void Duplicate_elements_clones_the_group_and_selects_the_copies()
    {
        var document = new EditorDocument("scene");
        var node = document.PlaceNode("a", "m.glb", EditorTransform.Identity);
        var lamp = document.AddNewPointLight(new Float3(2f, 1f, 0f));
        int depth = document.Commands.UndoDepth;

        document.DuplicateElements(new[] { SceneElementRef.Node(node), SceneElementRef.Light(lamp) })
            .Should().BeTrue();

        document.Scene.Count.Should().Be(2);
        document.Scene.LightCount.Should().Be(2);
        document.SelectedElements.Should().HaveCount(2, "the copies are the new selection");
        document.SelectedElements.Should().NotContain(SceneElementRef.Node(node));
        var nodeCopy = document.Scene.Find(document.SelectedElements[0].AsNode)!;
        nodeCopy.Name.Should().Be("a copy");
        nodeCopy.Transform.Position.X.Should().Be(1f, "the copy offsets one metre along X");
        var lampCopy = document.Scene.FindLight(document.SelectedElements[1].AsLight)!;
        lampCopy.Position.X.Should().Be(3f);
        document.Commands.UndoDepth.Should().Be(depth + 1, "the group duplicate is one history entry");

        document.Undo().Should().BeTrue();
        document.Scene.Count.Should().Be(1);
        document.Scene.LightCount.Should().Be(1);
    }

    [Fact]
    public void Loading_a_scene_clears_the_selection_set()
    {
        var document = new EditorDocument("scene");
        var first = document.PlaceNode("a", null, EditorTransform.Identity);
        document.PlaceNode("b", null, EditorTransform.Identity);
        document.ToggleSelect(SceneElementRef.Node(first));

        document.LoadScene(EditorSceneDocument.Empty("other"));

        document.SelectedElements.Should().BeEmpty();
    }

    [Fact]
    public void Preview_light_records_no_undo_step_and_commit_collapses_the_drag_to_one()
    {
        var document = new EditorDocument("scene");
        var id = document.AddNewPointLight(Float3.Zero);
        var start = document.Scene.FindLight(id)!;

        document.PreviewLight(start with { Position = new Float3(1f, 0f, 0f) }).Should().BeTrue();
        document.PreviewLight(start with { Position = new Float3(2f, 0f, 0f) }).Should().BeTrue();
        document.CommitLight(start, document.Scene.FindLight(id)!).Should().BeTrue();

        document.Scene.FindLight(id)!.Position.X.Should().Be(2f);
        document.Undo().Should().BeTrue();
        document.Scene.FindLight(id)!.Position.X.Should().Be(0f, "undo restores the grab-start value");
        document.Redo().Should().BeTrue();
        document.Scene.FindLight(id)!.Position.X.Should().Be(2f);
    }

    [Fact]
    public void Duplicate_light_clones_with_a_copy_suffix_an_offset_and_selection()
    {
        var document = new EditorDocument("scene");
        var id = document.AddNewPointLight(new Float3(1f, 2f, 3f));

        var copy = document.DuplicateLight(id);

        copy.IsValid.Should().BeTrue();
        var light = document.Scene.FindLight(copy)!;
        light.Name.Should().Be($"light {id.Value} copy");
        light.Position.Should().Be(new Float3(2f, 2f, 3f), "the copy is offset one metre along X");
        light.Kind.Should().Be(SceneLightKind.Point);
        document.LightSelection.Should().Be(copy);

        document.Undo().Should().BeTrue();
        document.Scene.LightCount.Should().Be(1);
    }

    [Fact]
    public void Duplicating_a_missing_light_returns_none()
    {
        var document = new EditorDocument("scene");

        document.DuplicateLight(new SceneLightId(9)).Should().Be(SceneLightId.None);
    }

    [Fact]
    public void Hiding_a_node_is_one_undoable_edit_that_keeps_the_selection()
    {
        var document = new EditorDocument("scene");
        var id = document.PlaceNode("tank", "models/tank.glb", EditorTransform.Identity);
        document.MarkSaved();

        document.SetNodeHidden(id, hidden: true).Should().BeTrue();

        document.Scene.Find(id)!.Hidden.Should().BeTrue();
        document.Selection.Should().Be(id, "hiding never touches the selection");
        document.IsDirty.Should().BeTrue();

        document.Undo().Should().BeTrue();
        document.Scene.Find(id)!.Hidden.Should().BeFalse();
        document.Redo().Should().BeTrue();
        document.Scene.Find(id)!.Hidden.Should().BeTrue();
    }

    [Fact]
    public void Renaming_the_document_is_one_undoable_edit_the_mirror_follows()
    {
        var document = new EditorDocument("Harbor");
        document.MarkSaved();

        document.RenameDocument("Bastion").Should().BeTrue();

        document.Name.Should().Be("Bastion");
        document.IsDirty.Should().BeTrue();
        document.ToPseudoCode().Should().StartWith("scene \"Bastion\"");

        document.Undo().Should().BeTrue();
        document.Name.Should().Be("Harbor");
        document.Redo().Should().BeTrue();
        document.Name.Should().Be("Bastion");
    }

    [Fact]
    public void Renaming_the_document_to_its_own_name_is_a_no_op()
    {
        var document = new EditorDocument("Harbor");
        document.MarkSaved();

        document.RenameDocument("Harbor").Should().BeFalse();

        document.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void Setting_visibility_no_ops_when_absent_or_unchanged()
    {
        var document = new EditorDocument("scene");
        var id = document.PlaceNode("tank", null, EditorTransform.Identity);

        document.SetNodeHidden(new SceneNodeId(9), hidden: true).Should().BeFalse("no such node");
        document.SetNodeHidden(id, hidden: false).Should().BeFalse("already visible");
    }
}
