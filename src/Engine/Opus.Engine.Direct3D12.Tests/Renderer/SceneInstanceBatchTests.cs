using System.Numerics;
using FluentAssertions;
using Opus.Engine.Renderer.Direct3D12.Assets;
using Opus.Engine.Renderer.Direct3D12.Scene;
using Xunit;

namespace Opus.Engine.Direct3D12.Tests.Renderer;

/// <summary>Headless coverage for <see cref="SceneInstanceBatch"/> — the pure pass that groups
/// a flat draw list into per-mesh GPU instance batches. No device is touched.</summary>
public sealed class SceneInstanceBatchTests
{
    [Fact]
    public void Build_empty_draw_list_yields_no_instances_or_batches()
    {
        var result = SceneInstanceBatch.Build(System.Array.Empty<SceneNodeDraw>());

        result.Instances.Should().BeEmpty();
        result.Batches.Should().BeEmpty();
    }

    [Fact]
    public void Build_groups_every_instance_of_one_mesh_into_a_single_batch()
    {
        var draws = new[]
        {
            new SceneNodeDraw(0, Matrix4x4.CreateTranslation(1f, 0f, 0f)),
            new SceneNodeDraw(0, Matrix4x4.CreateTranslation(2f, 0f, 0f)),
            new SceneNodeDraw(0, Matrix4x4.CreateTranslation(3f, 0f, 0f)),
        };

        var result = SceneInstanceBatch.Build(draws);

        result.Batches.Should().ContainSingle();
        result.Batches[0].Should().Be(new SceneMeshBatch(MeshIndex: 0, InstanceOffset: 0, InstanceCount: 3));
        result.Instances.Should().HaveCount(3);
    }

    [Fact]
    public void Build_emits_one_batch_per_distinct_mesh_in_first_seen_order()
    {
        var draws = new[]
        {
            new SceneNodeDraw(2, Matrix4x4.Identity),
            new SceneNodeDraw(5, Matrix4x4.Identity),
        };

        var result = SceneInstanceBatch.Build(draws);

        result.Batches.Should().HaveCount(2);
        result.Batches[0].MeshIndex.Should().Be(2);
        result.Batches[1].MeshIndex.Should().Be(5);
    }

    [Fact]
    public void Build_regroups_interleaved_meshes_into_contiguous_instance_slices()
    {
        var draws = new[]
        {
            new SceneNodeDraw(0, Matrix4x4.CreateTranslation(1f, 0f, 0f)),
            new SceneNodeDraw(1, Matrix4x4.CreateTranslation(2f, 0f, 0f)),
            new SceneNodeDraw(0, Matrix4x4.CreateTranslation(3f, 0f, 0f)),
        };

        var result = SceneInstanceBatch.Build(draws);

        // Mesh 0 owns a contiguous slice [0,2), mesh 1 follows at [2,3).
        result.Batches.Should().Equal(
            new SceneMeshBatch(0, 0, 2),
            new SceneMeshBatch(1, 2, 1));

        // Instances are reordered so each mesh's draws are adjacent, draw order preserved within.
        result.Instances[0].World.Translation.Should().Be(new Vector3(1f, 0f, 0f));
        result.Instances[1].World.Translation.Should().Be(new Vector3(3f, 0f, 0f));
        result.Instances[2].World.Translation.Should().Be(new Vector3(2f, 0f, 0f));
    }

    [Fact]
    public void Build_carries_each_draws_world_and_tint_into_its_instance_record()
    {
        var world = Matrix4x4.CreateRotationY(0.5f) * Matrix4x4.CreateTranslation(7f, 8f, 9f);
        var tint = new Vector4(0.2f, 0.4f, 0.6f, 1f);
        var draws = new[] { new SceneNodeDraw(3, world, tint) };

        var result = SceneInstanceBatch.Build(draws);

        result.Instances.Should().ContainSingle();
        result.Instances[0].World.Should().Be(world);
        result.Instances[0].BaseColorFactor.Should().Be(tint);
    }

    [Fact]
    public void Build_carries_each_draws_uv_offset_into_its_instance_record()
    {
        var uvOffset = new Vector2(0.25f, 0.75f);
        var draws = new[] { new SceneNodeDraw(3, Matrix4x4.Identity, Vector4.One, uvOffset) };

        var result = SceneInstanceBatch.Build(draws);

        result.Instances.Should().ContainSingle();
        result.Instances[0].UvOffset.Should().Be(uvOffset);
    }
}
