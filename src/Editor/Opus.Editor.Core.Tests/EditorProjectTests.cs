using FluentAssertions;
using Xunit;

namespace Opus.Editor.Core.Tests;

public sealed class EditorProjectTests
{
    [Fact]
    public void Adds_are_deduplicated()
    {
        var project = new EditorProject { Name = "demo" };

        project.AddScene("a.scene.json").Should().BeTrue();
        project.AddScene("a.scene.json").Should().BeFalse();

        project.Scenes.Should().ContainSingle();
    }

    [Fact]
    public void Remove_drops_a_reference()
    {
        var project = new EditorProject();
        project.AddContentRoot("assets");

        project.RemoveContentRoot("assets").Should().BeTrue();

        project.ContentRoots.Should().BeEmpty();
    }

    [Fact]
    public void Snapshot_then_load_round_trips_all_lists()
    {
        var project = new EditorProject { Name = "demo" };
        project.AddContentRoot("assets");
        project.AddScene("s.scene.json");
        project.AddAnimationGraph("g.animgraph.json");
        project.AddMaterialRoot("textures");

        var document = project.Snapshot();
        var restored = new EditorProject();
        restored.Load(document);

        restored.Name.Should().Be("demo");
        restored.ContentRoots.Should().ContainSingle(root => root == "assets");
        restored.Scenes.Should().ContainSingle(scene => scene == "s.scene.json");
        restored.AnimationGraphs.Should().ContainSingle(graph => graph == "g.animgraph.json");
        restored.MaterialRoots.Should().ContainSingle(root => root == "textures");
    }
}
