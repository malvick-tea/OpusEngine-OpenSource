using System.Collections.Generic;
using FluentAssertions;
using Opus.Foundation;
using Opus.Persistence.Settings;
using Xunit;

namespace Opus.Editor.Core.Tests;

public sealed class EditorSceneSerializerTests
{
    [Fact]
    public void Round_trips_a_scene_document()
    {
        var document = new EditorSceneDocument(
            "Harbor",
            new[]
            {
                new SceneNode(
                    new SceneNodeId(1),
                    "tank",
                    "models/tank.glb",
                    new EditorTransform(new Float3(10f, 0f, 5f), new Float3(0f, 90f, 0f), Float3.One)),
                new SceneNode(new SceneNodeId(2), "group", null, EditorTransform.Identity),
            });

        var json = EditorSceneSerializer.Serialize(document);
        var result = EditorSceneSerializer.Deserialize(json);

        result.IsOk.Should().BeTrue();
        result.Unwrap().Should().BeEquivalentTo(document);
    }

    [Fact]
    public void Malformed_json_is_a_typed_settings_corrupt_error()
    {
        var result = EditorSceneSerializer.Deserialize("{ this is not json");

        result.IsErr.Should().BeTrue();
        result.UnwrapErr().Code.Should().Be(ErrorCode.SettingsCorrupt);
    }

    [Fact]
    public void Serialized_scene_is_human_readable_camel_case_json()
    {
        var document = new EditorSceneDocument(
            "Harbor",
            new[] { new SceneNode(new SceneNodeId(1), "tank", "models/tank.glb", EditorTransform.Identity) });

        var json = EditorSceneSerializer.Serialize(document);

        json.Should().Contain("\"name\": \"Harbor\"");
        json.Should().Contain("models/tank.glb");
    }

    [Fact]
    public void Round_trips_a_scene_with_lights()
    {
        var document = new EditorSceneDocument(
            "Harbor",
            new[] { new SceneNode(new SceneNodeId(1), "tank", "models/tank.glb", EditorTransform.Identity) })
        {
            Lights = new[]
            {
                SceneLight.CreateDirectional("sun").WithId(new SceneLightId(1)),
                SceneLight.CreateSpot("torch").WithId(new SceneLightId(2)) with { Position = new Float3(1f, 2f, 3f) },
            },
        };

        var json = EditorSceneSerializer.Serialize(document);
        var result = EditorSceneSerializer.Deserialize(json);

        result.IsOk.Should().BeTrue();
        result.Unwrap().Should().BeEquivalentTo(document);
    }

    [Fact]
    public void Pre_lighting_scene_file_without_a_lights_property_loads_with_no_lights()
    {
        // A scene file authored before lights existed has no "lights" property at all. Modelled with a
        // stand-in record carrying only the pre-lights shape, tagged with the same schema version, so the
        // current reader must tolerate the absent property rather than reject the file or NRE on null.
        var legacy = new LegacyScene(
            "Harbor",
            new[] { new SceneNode(new SceneNodeId(1), "tank", "models/tank.glb", EditorTransform.Identity) });
        var json = JsonSettingsSerializer.Serialize(legacy, EditorSceneSerializer.SchemaVersion);
        json.Should().NotContain("lights", "the legacy file predates the lights field");

        var result = EditorSceneSerializer.Deserialize(json);

        result.IsOk.Should().BeTrue("an additive field must not break older scene files");
        result.Unwrap().Lights.Should().BeEmpty();
        result.Unwrap().Nodes.Should().HaveCount(1);
    }

    [Fact]
    public void Round_trips_hidden_elements()
    {
        var document = new EditorSceneDocument(
            "Harbor",
            new[]
            {
                new SceneNode(new SceneNodeId(1), "tank", "models/tank.glb", EditorTransform.Identity)
                    .WithHidden(true),
            })
        {
            Lights = new[] { SceneLight.CreatePoint("torch").WithId(new SceneLightId(1)).WithHidden(true) },
        };

        var result = EditorSceneSerializer.Deserialize(EditorSceneSerializer.Serialize(document));

        result.IsOk.Should().BeTrue();
        result.Unwrap().Nodes[0].Hidden.Should().BeTrue();
        result.Unwrap().Lights[0].Hidden.Should().BeTrue();
    }

    [Fact]
    public void Pre_visibility_scene_file_without_hidden_properties_loads_visible()
    {
        // A scene file authored before the hidden flag existed carries no such property on its nodes;
        // the additive init field must default to visible rather than reject the file.
        var legacy = new LegacyVisibilityScene(
            "Harbor",
            new[] { new LegacyNode(new SceneNodeId(1), "tank", "models/tank.glb", EditorTransform.Identity) });
        var json = JsonSettingsSerializer.Serialize(legacy, EditorSceneSerializer.SchemaVersion);
        json.Should().NotContain("hidden", "the legacy file predates the visibility field");

        var result = EditorSceneSerializer.Deserialize(json);

        result.IsOk.Should().BeTrue("an additive field must not break older scene files");
        result.Unwrap().Nodes[0].Hidden.Should().BeFalse();
    }

    [Fact]
    public void Round_trips_a_parented_node()
    {
        var document = new EditorSceneDocument(
            "Harbor",
            new[]
            {
                new SceneNode(new SceneNodeId(1), "group", null, EditorTransform.Identity),
                new SceneNode(new SceneNodeId(2), "box", "models/box.glb", EditorTransform.Identity)
                {
                    ParentId = new SceneNodeId(1),
                },
            });

        var result = EditorSceneSerializer.Deserialize(EditorSceneSerializer.Serialize(document));

        result.IsOk.Should().BeTrue();
        result.Unwrap().Should().BeEquivalentTo(document);
        result.Unwrap().Nodes[1].ParentId.Should().Be(new SceneNodeId(1));
    }

    [Fact]
    public void Pre_hierarchy_scene_file_without_a_parent_property_loads_every_node_as_a_root()
    {
        // A scene file authored before parenting existed carries no "parentId" property on its nodes; the
        // additive init field must default to null (a root) rather than reject the file.
        var legacy = new LegacyVisibilityScene(
            "Harbor",
            new[]
            {
                new LegacyNode(new SceneNodeId(1), "group", null, EditorTransform.Identity),
                new LegacyNode(new SceneNodeId(2), "box", "models/box.glb", EditorTransform.Identity),
            });
        var json = JsonSettingsSerializer.Serialize(legacy, EditorSceneSerializer.SchemaVersion);
        json.Should().NotContain("parentId", "the legacy file predates the hierarchy field");

        var result = EditorSceneSerializer.Deserialize(json);

        result.IsOk.Should().BeTrue("an additive field must not break older scene files");
        result.Unwrap().Nodes.Should().OnlyContain(node => node.ParentId == null);
    }

    private sealed record LegacyScene(string Name, IReadOnlyList<SceneNode> Nodes);

    private sealed record LegacyNode(SceneNodeId Id, string Name, string? AssetRef, EditorTransform Transform);

    private sealed record LegacyVisibilityScene(string Name, IReadOnlyList<LegacyNode> Nodes);
}
