using System;
using System.Collections.Generic;
using System.Numerics;
using FluentAssertions;
using Opus.Engine.Renderer.Direct3D12.Assets;
using Opus.Engine.Renderer.Direct3D12.Scene;
using Opus.Foundation.Geometry;
using Xunit;

namespace Opus.Engine.Direct3D12.Tests.Renderer;

/// <summary>Headless coverage for <see cref="SceneLodSelector"/> — the CPU coarse-LOD pass that
/// reselects a distant node to a cheaper mesh variant between culling and instance-batching. No
/// GPU device is touched; selection is pure arithmetic over <see cref="Aabb"/> + the LOD chain.
/// Mesh 0 is the fine variant, mesh 1 the coarse one; the camera sits at the origin.</summary>
public sealed class SceneLodSelectorTests
{
    private static readonly Aabb UnitCube = new(new Vector3(-0.5f), new Vector3(0.5f));
    private static readonly Vector3 CameraAtOrigin = Vector3.Zero;

    // Two-level chain on mesh 0: fine (mesh 0) within 10 units, coarse (mesh 1) beyond.
    private static IReadOnlyList<SceneMeshLod> TwoLevelLodOnMesh0() => new[]
    {
        SceneMeshLod.Create(
            new SceneMeshLodLevel(MeshIndex: 0, MaxCameraDistance: 10f),
            new SceneMeshLodLevel(MeshIndex: 1, MaxCameraDistance: float.PositiveInfinity)),
        SceneMeshLod.None,
    };

    private static IReadOnlyList<Aabb> TwoMeshBounds() => new[] { UnitCube, UnitCube };

    private static SceneNodeDraw DrawInFront(float distance, int meshIndex = 0) =>
        new(meshIndex, Matrix4x4.CreateTranslation(0f, 0f, -distance));

    [Fact]
    public void Select_with_null_lods_returns_the_input_list_unchanged()
    {
        var draws = new[] { DrawInFront(5f) };

        var result = SceneLodSelector.Select(draws, meshLods: null, TwoMeshBounds(), CameraAtOrigin);

        result.DemotedNodeCount.Should().Be(0);
        result.Draws.Should().BeSameAs(draws, "no LOD chains means no work and no allocation");
    }

    [Fact]
    public void Select_with_null_bounds_returns_the_input_list_unchanged()
    {
        // LOD needs the bounds for the camera distance; with none it cannot measure, so it no-ops.
        var draws = new[] { DrawInFront(5f) };

        var result = SceneLodSelector.Select(draws, TwoLevelLodOnMesh0(), meshLocalBounds: null, CameraAtOrigin);

        result.DemotedNodeCount.Should().Be(0);
        result.Draws.Should().BeSameAs(draws);
    }

    [Fact]
    public void Select_keeps_a_near_node_at_its_finest_level()
    {
        var draws = new[] { DrawInFront(5f) };

        var result = SceneLodSelector.Select(draws, TwoLevelLodOnMesh0(), TwoMeshBounds(), CameraAtOrigin);

        result.DemotedNodeCount.Should().Be(0);
        result.Draws.Should().ContainSingle().Which.MeshIndex.Should().Be(0, "5 units is inside the fine threshold");
    }

    [Fact]
    public void Select_reselects_a_far_node_to_the_coarse_level()
    {
        var draws = new[] { DrawInFront(50f) };

        var result = SceneLodSelector.Select(draws, TwoLevelLodOnMesh0(), TwoMeshBounds(), CameraAtOrigin);

        result.DemotedNodeCount.Should().Be(1);
        result.Draws.Should().ContainSingle().Which.MeshIndex.Should().Be(1, "50 units is beyond the fine threshold");
    }

    [Fact]
    public void Select_picks_the_middle_level_of_a_three_level_chain()
    {
        IReadOnlyList<SceneMeshLod> lods = new[]
        {
            SceneMeshLod.Create(
                new SceneMeshLodLevel(0, 10f),
                new SceneMeshLodLevel(1, 100f),
                new SceneMeshLodLevel(2, float.PositiveInfinity)),
        };
        IReadOnlyList<Aabb> bounds = new[] { UnitCube };
        var draws = new[] { DrawInFront(50f) };

        var result = SceneLodSelector.Select(draws, lods, bounds, CameraAtOrigin);

        result.Draws.Should().ContainSingle().Which.MeshIndex.Should().Be(1, "50 units is past level 0 but within level 1");
    }

    [Fact]
    public void Select_keeps_a_node_whose_mesh_has_an_empty_chain()
    {
        // Mesh 1 carries SceneMeshLod.None — a far node on it must stay on mesh 1.
        var draws = new[] { DrawInFront(500f, meshIndex: 1) };

        var result = SceneLodSelector.Select(draws, TwoLevelLodOnMesh0(), TwoMeshBounds(), CameraAtOrigin);

        result.DemotedNodeCount.Should().Be(0);
        result.Draws.Should().ContainSingle().Which.MeshIndex.Should().Be(1);
    }

    [Fact]
    public void Select_keeps_a_node_whose_mesh_index_has_no_lod_entry()
    {
        // MeshIndex 4 with only two LOD entries -> out of range, kept at full detail.
        var draws = new[] { DrawInFront(500f, meshIndex: 4) };

        var result = SceneLodSelector.Select(draws, TwoLevelLodOnMesh0(), TwoMeshBounds(), CameraAtOrigin);

        result.DemotedNodeCount.Should().Be(0);
        result.Draws.Should().ContainSingle().Which.MeshIndex.Should().Be(4);
    }

    [Fact]
    public void Select_keeps_a_node_whose_mesh_bounds_are_empty()
    {
        // Empty bounds = cannot measure distance -> never reselect, even when far.
        IReadOnlyList<Aabb> bounds = new[] { Aabb.Empty, UnitCube };
        var draws = new[] { DrawInFront(500f) };

        var result = SceneLodSelector.Select(draws, TwoLevelLodOnMesh0(), bounds, CameraAtOrigin);

        result.DemotedNodeCount.Should().Be(0);
        result.Draws.Should().ContainSingle().Which.MeshIndex.Should().Be(0);
    }

    [Fact]
    public void Select_uses_the_nearest_point_so_a_camera_inside_the_box_stays_finest()
    {
        // A box the camera sits inside has nearest-point distance 0 -> finest level, even though
        // the box is enormous (its centre is at the origin too).
        IReadOnlyList<Aabb> bounds = new[] { new Aabb(new Vector3(-1000f), new Vector3(1000f)) };
        IReadOnlyList<SceneMeshLod> lods = new[] { TwoLevelLodOnMesh0()[0] };
        var draws = new[] { new SceneNodeDraw(0, Matrix4x4.Identity) };

        var result = SceneLodSelector.Select(draws, lods, bounds, CameraAtOrigin);

        result.DemotedNodeCount.Should().Be(0);
        result.Draws.Should().ContainSingle().Which.MeshIndex.Should().Be(0);
    }

    [Fact]
    public void Select_measures_distance_from_the_world_aabb_not_the_local_one()
    {
        // Local bounds sit at the origin; the node's world transform pushes them far away, so the
        // distance must come from the transformed box, demoting the node to coarse.
        IReadOnlyList<SceneMeshLod> lods = new[] { TwoLevelLodOnMesh0()[0] };
        IReadOnlyList<Aabb> bounds = new[] { UnitCube };
        var draws = new[] { new SceneNodeDraw(0, Matrix4x4.CreateTranslation(80f, 0f, 0f)) };

        var result = SceneLodSelector.Select(draws, lods, bounds, CameraAtOrigin);

        result.Draws.Should().ContainSingle().Which.MeshIndex.Should().Be(1);
    }

    [Fact]
    public void Select_preserves_world_and_tint_when_it_reselects_the_mesh()
    {
        var world = Matrix4x4.CreateTranslation(0f, 3f, -50f);
        var tint = new Vector4(0.2f, 0.4f, 0.6f, 1f);
        var draws = new[] { new SceneNodeDraw(0, world, tint) };

        var result = SceneLodSelector.Select(draws, TwoLevelLodOnMesh0(), TwoMeshBounds(), CameraAtOrigin);

        var resolved = result.Draws.Should().ContainSingle().Subject;
        resolved.MeshIndex.Should().Be(1, "the far node is reselected to the coarse mesh");
        resolved.World.Should().Be(world, "LOD only rewrites the mesh, never the transform");
        resolved.TintFactor.Should().Be(tint, "LOD only rewrites the mesh, never the tint");
    }

    [Fact]
    public void Select_counts_only_the_nodes_reselected_below_their_finest_level()
    {
        var draws = new List<SceneNodeDraw>
        {
            DrawInFront(3f),    // near  -> stays mesh 0
            DrawInFront(60f),   // far   -> mesh 1
            DrawInFront(80f),   // far   -> mesh 1
        };

        var result = SceneLodSelector.Select(draws, TwoLevelLodOnMesh0(), TwoMeshBounds(), CameraAtOrigin);

        result.DemotedNodeCount.Should().Be(2);
        result.Draws[0].MeshIndex.Should().Be(0);
        result.Draws[1].MeshIndex.Should().Be(1);
        result.Draws[2].MeshIndex.Should().Be(1);
    }

    [Fact]
    public void Create_rejects_an_empty_or_unordered_or_negative_chain()
    {
        var empty = () => SceneMeshLod.Create();
        empty.Should().Throw<ArgumentException>("a LOD chain needs at least one level");

        var unordered = () => SceneMeshLod.Create(
            new SceneMeshLodLevel(0, 100f),
            new SceneMeshLodLevel(1, 10f));
        unordered.Should().Throw<ArgumentException>("levels must ascend by distance, finest first");

        var negative = () => SceneMeshLod.Create(new SceneMeshLodLevel(-1, 10f));
        negative.Should().Throw<ArgumentOutOfRangeException>("a level mesh index must be non-negative");

        var valid = SceneMeshLod.Create(
            new SceneMeshLodLevel(0, 10f),
            new SceneMeshLodLevel(1, float.PositiveInfinity));
        valid.HasLevels.Should().BeTrue();
        valid.Levels.Should().HaveCount(2);
    }
}
