using System.Collections.Generic;
using System.Numerics;
using FluentAssertions;
using Opus.Engine.Renderer.Direct3D12.Assets;
using Opus.Engine.Renderer.Direct3D12.Scene;
using Opus.Foundation.Geometry;
using Xunit;

namespace Opus.Engine.Direct3D12.Tests.Renderer;

/// <summary>Headless coverage for <see cref="SceneNodeCuller"/> — the CPU frustum-cull
/// pass that drops off-screen scene nodes before the draw loop. No GPU device is touched;
/// the culler is pure arithmetic over <see cref="Frustum"/> + <see cref="Aabb"/>.</summary>
public sealed class SceneNodeCullerTests
{
    private static readonly Aabb UnitCube = new(new Vector3(-0.5f), new Vector3(0.5f));

    [Fact]
    public void Cull_with_null_bounds_returns_the_input_list_unchanged()
    {
        var draws = new[] { new SceneNodeDraw(0, Matrix4x4.Identity) };
        var frustum = BuildFrustum();

        var result = SceneNodeCuller.Cull(draws, meshLocalBounds: null, in frustum);

        result.CulledCount.Should().Be(0);
        result.Visible.Should().BeSameAs(draws, "a null-bounds cull must allocate nothing and keep every node");
    }

    [Fact]
    public void Cull_keeps_a_node_inside_the_frustum()
    {
        var draws = new[] { new SceneNodeDraw(0, Matrix4x4.Identity) };
        var frustum = BuildFrustum();

        var result = SceneNodeCuller.Cull(draws, new[] { UnitCube }, in frustum);

        result.CulledCount.Should().Be(0);
        result.Visible.Should().ContainSingle();
    }

    [Fact]
    public void Cull_drops_a_node_fully_outside_the_frustum()
    {
        // Same unit-cube mesh, shoved 1000 units to the right of a camera that looks down -Z.
        var draws = new[] { new SceneNodeDraw(0, Matrix4x4.CreateTranslation(1000f, 0f, 0f)) };
        var frustum = BuildFrustum();

        var result = SceneNodeCuller.Cull(draws, new[] { UnitCube }, in frustum);

        result.CulledCount.Should().Be(1);
        result.Visible.Should().BeEmpty();
    }

    [Fact]
    public void Cull_keeps_a_node_whose_mesh_bounds_are_empty()
    {
        // Empty bounds = "cannot bound this mesh" — the culler must never drop such a node,
        // even when its transform would place a real box far outside the frustum.
        var draws = new[] { new SceneNodeDraw(0, Matrix4x4.CreateTranslation(1000f, 0f, 0f)) };
        var frustum = BuildFrustum();

        var result = SceneNodeCuller.Cull(draws, new[] { Aabb.Empty }, in frustum);

        result.CulledCount.Should().Be(0);
        result.Visible.Should().ContainSingle();
    }

    [Fact]
    public void Cull_keeps_a_node_whose_mesh_index_has_no_bounds_entry()
    {
        // MeshIndex 4 with only one bounds entry — out of range, so the node is kept.
        var draws = new[] { new SceneNodeDraw(4, Matrix4x4.CreateTranslation(1000f, 0f, 0f)) };
        var frustum = BuildFrustum();

        var result = SceneNodeCuller.Cull(draws, new[] { UnitCube }, in frustum);

        result.CulledCount.Should().Be(0);
        result.Visible.Should().ContainSingle();
    }

    [Fact]
    public void Cull_keeps_the_visible_node_and_drops_the_off_screen_one()
    {
        var draws = new List<SceneNodeDraw>
        {
            new(0, Matrix4x4.Identity),                          // at origin -> visible
            new(0, Matrix4x4.CreateTranslation(1000f, 0f, 0f)),  // far right -> culled
        };
        var frustum = BuildFrustum();

        var result = SceneNodeCuller.Cull(draws, new[] { UnitCube }, in frustum);

        result.CulledCount.Should().Be(1);
        result.Visible.Should().ContainSingle()
            .Which.World.Translation.Should().Be(Vector3.Zero);
    }

    private static Frustum BuildFrustum()
    {
        var view = Matrix4x4.CreateLookAt(new Vector3(0f, 0f, 10f), Vector3.Zero, Vector3.UnitY);
        var proj = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3f, 16f / 9f, 0.1f, 100f);
        return Frustum.FromViewProjection(view * proj);
    }
}
