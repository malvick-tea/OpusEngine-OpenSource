using System;
using FluentAssertions;
using Opus.Editor.Core;
using Xunit;

namespace Opus.Editor.Content.Tests;

public sealed class SceneContentReporterTests
{
    private static ModelInspection Fake(string path, int vertices, int triangles) => new(
        path, 1, 1, vertices, triangles, 1, 1, 1, false, false, Float3.Zero, Float3.Zero, Array.Empty<ModelMeshSummary>());

    [Fact]
    public void Aggregates_instances_and_geometry_per_asset()
    {
        var scene = new EditorScene();
        scene.Add(new SceneNode(scene.AllocateId(), "a", "tank.glb", EditorTransform.Identity));
        scene.Add(new SceneNode(scene.AllocateId(), "b", "tank.glb", EditorTransform.Identity));
        scene.Add(new SceneNode(scene.AllocateId(), "c", "tree.glb", EditorTransform.Identity));

        var report = SceneContentReporter.Build(scene, asset => asset == "tank.glb" ? Fake(asset, 100, 50) : null);

        report.NodeCount.Should().Be(3);
        report.DistinctAssetCount.Should().Be(2);
        report.ResolvedAssetCount.Should().Be(1);
        report.MissingAssetCount.Should().Be(1);
        report.TotalVertices.Should().Be(200);
        report.TotalTriangles.Should().Be(100);
    }

    [Fact]
    public void Grouping_nodes_without_assets_count_as_nodes_only()
    {
        var scene = new EditorScene();
        scene.Add(new SceneNode(scene.AllocateId(), "group", null, EditorTransform.Identity));

        var report = SceneContentReporter.Build(scene, _ => null);

        report.NodeCount.Should().Be(1);
        report.DistinctAssetCount.Should().Be(0);
    }

    [Fact]
    public void A_missing_asset_is_flagged_unresolved()
    {
        var scene = new EditorScene();
        scene.Add(new SceneNode(scene.AllocateId(), "a", "ghost.glb", EditorTransform.Identity));

        var report = SceneContentReporter.Build(scene, _ => null);

        report.Assets.Should().ContainSingle(usage => usage.AssetRef == "ghost.glb" && !usage.Resolved);
    }

    [Fact]
    public void Tallies_lights_by_kind()
    {
        var scene = new EditorScene();
        scene.AddLight(SceneLight.CreateDirectional("sun").WithId(scene.AllocateLightId()));
        scene.AddLight(SceneLight.CreatePoint("lamp").WithId(scene.AllocateLightId()));
        scene.AddLight(SceneLight.CreateSpot("torch").WithId(scene.AllocateLightId()));
        scene.AddLight(SceneLight.CreatePoint("lamp2").WithId(scene.AllocateLightId()));

        var report = SceneContentReporter.Build(scene, _ => null);

        report.Lights.Directional.Should().Be(1);
        report.Lights.Point.Should().Be(2);
        report.Lights.Spot.Should().Be(1);
        report.Lights.Total.Should().Be(4);
    }
}
