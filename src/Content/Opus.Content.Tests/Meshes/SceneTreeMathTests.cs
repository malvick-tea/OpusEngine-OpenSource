using System.Numerics;
using FluentAssertions;
using Opus.Content.Meshes;
using Xunit;

namespace Opus.Content.Tests.Meshes;

public sealed class SceneTreeMathTests
{
    private static readonly int[] RootNodeIndices = { 0 };
    private static readonly int[] ChildOneIndices = { 1 };
    private static readonly int[] ChildZeroIndices = { 0 };

    [Fact]
    public void ComputeWorldTransforms_multiplies_child_local_by_parent_world()
    {
        var scene = new GltfScene(
            new[]
            {
                new GltfNode("root", -1, ChildOneIndices, Matrix4x4.CreateTranslation(10f, 0f, 0f), null),
                new GltfNode("child", 0, Array.Empty<int>(), Matrix4x4.CreateTranslation(0f, 5f, 0f), null),
            },
            RootNodeIndices,
            Array.Empty<GltfMesh>());

        var worlds = SceneTreeMath.ComputeWorldTransforms(scene);

        worlds[0].M41.Should().BeApproximately(10f, 1e-5f);
        worlds[0].M42.Should().BeApproximately(0f, 1e-5f);
        worlds[1].M41.Should().BeApproximately(10f, 1e-5f);
        worlds[1].M42.Should().BeApproximately(5f, 1e-5f);
    }

    [Fact]
    public void ComputeWorldTransforms_rejects_cycles_reachable_from_roots()
    {
        var scene = new GltfScene(
            new[]
            {
                new GltfNode("a", 1, ChildOneIndices, Matrix4x4.Identity, null),
                new GltfNode("b", 0, ChildZeroIndices, Matrix4x4.Identity, null),
            },
            RootNodeIndices,
            Array.Empty<GltfMesh>());

        var act = () => SceneTreeMath.ComputeWorldTransforms(scene);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cycle*");
    }

    [Fact]
    public void ComputeWorldTransforms_can_pose_one_local_transform_at_runtime()
    {
        var scene = new GltfScene(
            new[]
            {
                new GltfNode("root", -1, ChildOneIndices, Matrix4x4.CreateTranslation(10f, 0f, 0f), null),
                new GltfNode("child", 0, Array.Empty<int>(), Matrix4x4.CreateTranslation(0f, 5f, 0f), null),
            },
            RootNodeIndices,
            Array.Empty<GltfMesh>());
        var locals = new[]
        {
            scene.Nodes[0].LocalTransform,
            Matrix4x4.CreateTranslation(0f, 7f, 0f),
        };

        var worlds = SceneTreeMath.ComputeWorldTransforms(scene, locals);

        worlds[1].Translation.Should().Be(new Vector3(10f, 7f, 0f));
    }
}
