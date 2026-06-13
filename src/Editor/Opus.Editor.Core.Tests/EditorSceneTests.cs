using System;
using FluentAssertions;
using Xunit;

namespace Opus.Editor.Core.Tests;

public sealed class EditorSceneTests
{
    private static SceneNode NodeFor(EditorScene scene, string name) =>
        new(scene.AllocateId(), name, null, EditorTransform.Identity);

    [Fact]
    public void Allocates_dense_ascending_ids()
    {
        var scene = new EditorScene();

        scene.AllocateId().Value.Should().Be(1);
        scene.AllocateId().Value.Should().Be(2);
    }

    [Fact]
    public void Add_find_remove_round_trip()
    {
        var scene = new EditorScene();
        var node = NodeFor(scene, "a");

        scene.Add(node);

        scene.Find(node.Id).Should().Be(node);
        scene.IndexOf(node.Id).Should().Be(0);
        scene.RemoveAt(0).Should().Be(node);
        scene.Contains(node.Id).Should().BeFalse();
    }

    [Fact]
    public void Insert_restores_position()
    {
        var scene = new EditorScene();
        var a = NodeFor(scene, "a");
        var b = NodeFor(scene, "b");
        scene.Add(a);
        scene.Add(b);

        scene.RemoveAt(0);
        scene.Insert(0, a);

        scene.Nodes[0].Should().Be(a);
        scene.Nodes[1].Should().Be(b);
    }

    [Fact]
    public void Adding_a_duplicate_id_throws()
    {
        var scene = new EditorScene();
        var node = new SceneNode(new SceneNodeId(1), "a", null, EditorTransform.Identity);
        scene.Add(node);

        var act = () => scene.Add(node);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Replace_swaps_node_in_place()
    {
        var scene = new EditorScene();
        var node = NodeFor(scene, "a");
        scene.Add(node);

        scene.Replace(node.WithName("b"));

        scene.Find(node.Id)!.Name.Should().Be("b");
    }

    [Fact]
    public void Load_sets_name_nodes_and_next_id()
    {
        var scene = new EditorScene();
        var document = new EditorSceneDocument(
            "Harbor",
            new[] { new SceneNode(new SceneNodeId(7), "x", null, EditorTransform.Identity) });

        scene.Load(document);

        scene.Name.Should().Be("Harbor");
        scene.Count.Should().Be(1);
        scene.AllocateId().Value.Should().Be(8);
    }

    [Fact]
    public void Snapshot_is_independent_of_later_edits()
    {
        var scene = new EditorScene { Name = "s" };
        scene.Add(NodeFor(scene, "a"));

        var snapshot = scene.Snapshot();
        scene.Add(NodeFor(scene, "b"));

        snapshot.Nodes.Should().HaveCount(1);
    }

    [Fact]
    public void Node_and_light_ids_are_independent_sequences()
    {
        var scene = new EditorScene();

        scene.AllocateId().Value.Should().Be(1);
        scene.AllocateLightId().Value.Should().Be(1, "lights count from their own sequence, not the node one");
        scene.AllocateLightId().Value.Should().Be(2);
        scene.AllocateId().Value.Should().Be(2);
    }

    [Fact]
    public void Light_add_find_remove_round_trip()
    {
        var scene = new EditorScene();
        var light = SceneLight.CreatePoint("lamp").WithId(scene.AllocateLightId());

        scene.AddLight(light);

        scene.FindLight(light.Id).Should().Be(light);
        scene.IndexOfLight(light.Id).Should().Be(0);
        scene.LightCount.Should().Be(1);
        scene.RemoveLightAt(0).Should().Be(light);
        scene.ContainsLight(light.Id).Should().BeFalse();
    }

    [Fact]
    public void Replace_light_swaps_in_place()
    {
        var scene = new EditorScene();
        var light = SceneLight.CreateSpot("torch").WithId(scene.AllocateLightId());
        scene.AddLight(light);

        scene.ReplaceLight(light.WithName("key"));

        scene.FindLight(light.Id)!.Name.Should().Be("key");
    }

    [Fact]
    public void Snapshot_includes_lights()
    {
        var scene = new EditorScene { Name = "s" };
        scene.AddLight(SceneLight.CreateDirectional("sun").WithId(scene.AllocateLightId()));

        scene.Snapshot().Lights.Should().HaveCount(1);
    }

    [Fact]
    public void Load_restores_lights_and_next_light_id()
    {
        var scene = new EditorScene();
        var document = EditorSceneDocument.Empty("Harbor") with
        {
            Lights = new[] { SceneLight.CreatePoint("lamp").WithId(new SceneLightId(7)) },
        };

        scene.Load(document);

        scene.LightCount.Should().Be(1);
        scene.AllocateLightId().Value.Should().Be(8);
    }
}
