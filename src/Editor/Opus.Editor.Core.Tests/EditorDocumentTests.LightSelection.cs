using FluentAssertions;
using Xunit;

namespace Opus.Editor.Core.Tests;

public sealed partial class EditorDocumentTests
{
    [Fact]
    public void Add_new_point_light_names_it_after_its_id_positions_it_and_is_undoable()
    {
        var document = new EditorDocument("scene");

        var id = document.AddNewPointLight(new Float3(1f, 2f, 3f));

        var light = document.Scene.FindLight(id)!;
        light.Name.Should().Be($"light {id.Value}");
        light.Kind.Should().Be(SceneLightKind.Point);
        light.Position.Should().Be(new Float3(1f, 2f, 3f));
        document.IsDirty.Should().BeTrue();

        document.Undo().Should().BeTrue();
        document.Scene.LightCount.Should().Be(0);
    }

    [Fact]
    public void Selecting_a_light_clears_the_node_selection_and_vice_versa()
    {
        var document = new EditorDocument("scene");
        var node = document.PlaceNode("a", null, EditorTransform.Identity);
        var lamp = document.AddNewPointLight(Float3.Zero);

        document.LightSelection.Should().Be(lamp, "adding a light from the window selects it");
        document.Selection.Should().Be(SceneNodeId.None);
        document.SelectedElement.Should().Be(SceneElementRef.Light(lamp));

        document.Select(node);

        document.Selection.Should().Be(node);
        document.LightSelection.Should().Be(SceneLightId.None);
        document.SelectedElement.Should().Be(SceneElementRef.Node(node));
    }

    [Fact]
    public void Removing_the_selected_light_clears_the_light_selection()
    {
        var document = new EditorDocument("scene");
        var lamp = document.AddNewPointLight(Float3.Zero);

        document.RemoveLight(lamp).Should().BeTrue();

        document.LightSelection.Should().Be(SceneLightId.None);
        document.SelectedElement.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Undoing_a_light_add_clamps_the_light_selection()
    {
        var document = new EditorDocument("scene");
        document.AddNewPointLight(Float3.Zero);

        document.Undo().Should().BeTrue();

        document.LightSelection.Should().Be(SceneLightId.None);
        document.SelectedElement.IsValid.Should().BeFalse();
    }
}
