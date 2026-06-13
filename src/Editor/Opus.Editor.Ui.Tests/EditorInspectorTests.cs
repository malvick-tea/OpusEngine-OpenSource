using System.Linq;
using FluentAssertions;
using Opus.Editor.Core;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class EditorInspectorTests
{
    private static readonly EditorPanelRect Panel = new(960, 32, 320, 300);

    private static EditorScene WithNode(out SceneNodeId id)
    {
        var scene = new EditorScene();
        id = scene.AllocateId();
        scene.Add(new SceneNode(
            id,
            "tank",
            "models/tank.glb",
            new EditorTransform(new Float3(1.25f, 0f, -3f), new Float3(0f, 45f, 0f), Float3.One)));
        return scene;
    }

    [Fact]
    public void A_selected_node_lists_name_asset_and_nine_numeric_rows()
    {
        var scene = WithNode(out var id);

        var rows = EditorInspector.Build(Panel, scene, SceneElementRef.Node(id));

        rows.Should().HaveCount(11);
        rows[0].Label.Should().Be("name");
        rows[0].Value.Should().Be("tank");
        rows[0].Editable.Should().BeTrue("clicking the name starts a rename");
        rows[1].Label.Should().Be("asset");
        rows[1].Editable.Should().BeFalse();
        rows.Single(r => r.Field == InspectorField.PositionX).Value.Should().Be("1.25");
        rows.Single(r => r.Field == InspectorField.RotationY).Value.Should().Be("45");
        rows.Single(r => r.Field == InspectorField.ScaleZ).Value.Should().Be("1");
    }

    [Fact]
    public void A_directional_light_hides_position_range_and_cone_rows()
    {
        var scene = new EditorScene();
        var id = scene.AllocateLightId();
        scene.AddLight(SceneLight.CreateDirectional("sun").WithId(id));

        var rows = EditorInspector.Build(Panel, scene, SceneElementRef.Light(id));

        rows.Select(r => r.Field).Should().NotContain(new[]
        {
            InspectorField.PositionX, InspectorField.Range, InspectorField.SpotInner, InspectorField.SpotOuter,
        });
        rows.Should().Contain(r => r.Field == InspectorField.DirectionY);
        rows.Should().Contain(r => r.Field == InspectorField.Intensity);
    }

    [Fact]
    public void A_spot_light_lists_the_full_fifteen_rows()
    {
        var scene = new EditorScene();
        var id = scene.AllocateLightId();
        scene.AddLight(SceneLight.CreateSpot("torch").WithId(id));

        var rows = EditorInspector.Build(Panel, scene, SceneElementRef.Light(id));

        rows.Should().HaveCount(15);
        rows.Single(r => r.Field == InspectorField.Kind).Value.Should().Be("spot");
        rows.Single(r => r.Field == InspectorField.SpotOuter).Value.Should().Be("30");
    }

    [Fact]
    public void No_selection_shows_the_scene_document_rows()
    {
        var scene = new EditorScene { Name = "Harbor" };
        scene.Add(new SceneNode(scene.AllocateId(), "tank", null, EditorTransform.Identity));
        scene.AddLight(SceneLight.CreatePoint("torch").WithId(scene.AllocateLightId()));

        var rows = EditorInspector.Build(Panel, scene, SceneElementRef.None);

        rows.Should().HaveCount(3);
        var name = rows[0];
        name.Field.Should().Be(InspectorField.Name, "clicking the scene name starts the document rename");
        name.Value.Should().Be("Harbor");
        name.Editable.Should().BeTrue();
        rows[1].Value.Should().Be("1");
        rows[2].Value.Should().Be("1");
        EditorInspector.HitTest(rows, rows[1].Rect.X + 4, rows[1].Rect.Y + 4)
            .Should().Be(InspectorField.None, "the count rows are display-only");
    }

    [Fact]
    public void The_editing_row_shows_the_buffer_with_a_caret()
    {
        var scene = WithNode(out var id);
        var edit = new FieldEditState(SceneElementRef.Node(id), InspectorField.PositionX, "2.5");

        var rows = EditorInspector.Build(Panel, scene, SceneElementRef.Node(id), edit);

        var editing = rows.Single(r => r.Editing);
        editing.Field.Should().Be(InspectorField.PositionX);
        editing.Value.Should().Be("2.5" + EditorInspector.EditCaret);
    }

    [Fact]
    public void Hit_test_returns_editable_rows_only()
    {
        var scene = WithNode(out var id);
        var rows = EditorInspector.Build(Panel, scene, SceneElementRef.Node(id));
        var asset = rows.Single(r => r.Field == InspectorField.Asset).Rect;
        var posX = rows.Single(r => r.Field == InspectorField.PositionX).Rect;

        EditorInspector.HitTest(rows, posX.X + 4, posX.Y + 4).Should().Be(InspectorField.PositionX);
        EditorInspector.HitTest(rows, asset.X + 4, asset.Y + 4).Should().Be(InspectorField.None);
        EditorInspector.HitTest(rows, Panel.X + 4, Panel.Bottom + 10).Should().Be(InspectorField.None);
    }

    [Fact]
    public void Rows_clip_at_the_panel_bottom()
    {
        var scene = WithNode(out var id);
        var shortPanel = new EditorPanelRect(960, 32, 320, EditorInspector.HeaderHeight + (3 * EditorInspector.RowHeight));

        var rows = EditorInspector.Build(shortPanel, scene, SceneElementRef.Node(id));

        rows.Should().HaveCount(3);
    }

    [Fact]
    public void Values_format_invariant_with_up_to_three_decimals()
    {
        EditorInspector.Format(1.23456f).Should().Be("1.235");
        EditorInspector.Format(-0.5f).Should().Be("-0.5");
        EditorInspector.Format(10f).Should().Be("10");
    }
}
