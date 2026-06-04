using FluentAssertions;
using Opus.Content.Meshes;
using Opus.Content.Tests.Fixtures;
using Xunit;

namespace Opus.Content.Tests.Meshes;

public sealed class GltfFilePackerTests
{
    [Fact]
    public void PackToGlb_packs_split_gltf_and_bin_into_readable_scene()
    {
        var tempDir = Directory.CreateTempSubdirectory("opus-content-pack-");
        try
        {
            var (json, bin) = GltfTestAssets.SplitTriangleScene();
            var gltfPath = Path.Combine(tempDir.FullName, "scene.gltf");
            File.WriteAllText(gltfPath, json);
            File.WriteAllBytes(Path.Combine(tempDir.FullName, "scene.bin"), bin);

            var glb = GltfFilePacker.PackToGlb(gltfPath);
            var scene = GltfBinaryReader.ReadScene(glb);

            scene.RootNodes.Should().Equal(0);
            scene.Nodes.Should().HaveCount(2);
            scene.Nodes[1].Name.Should().Be("gun");
            scene.Meshes.Should().ContainSingle();
            scene.Meshes[0].Primitives.Should().ContainSingle();

            var primitive = scene.Meshes[0].Primitives[0];
            primitive.MaterialIndex.Should().Be(0);
            primitive.Geometry.Name.Should().Be("triangle");
            primitive.Geometry.Positions.Should().Equal(
                new System.Numerics.Vector3(0f, 0f, 0f),
                new System.Numerics.Vector3(1f, 0f, 0f),
                new System.Numerics.Vector3(0f, 1f, 0f));
            primitive.Geometry.Indices.Should().Equal(0u, 1u, 2u);
            primitive.Geometry.Uvs.Should().NotBeNull();
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [Fact]
    public void PackToGlb_rejects_missing_sidecar_bin()
    {
        var tempDir = Directory.CreateTempSubdirectory("opus-content-pack-missing-");
        try
        {
            var (json, _) = GltfTestAssets.SplitTriangleScene();
            var gltfPath = Path.Combine(tempDir.FullName, "scene.gltf");
            File.WriteAllText(gltfPath, json);

            var act = () => GltfFilePacker.PackToGlb(gltfPath);

            act.Should().Throw<FileNotFoundException>();
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [Fact]
    public void PackToGlb_with_a_budget_packs_a_scene_within_budget()
    {
        var tempDir = Directory.CreateTempSubdirectory("opus-content-pack-budget-");
        try
        {
            var (json, bin) = GltfTestAssets.SplitTriangleScene();
            var gltfPath = Path.Combine(tempDir.FullName, "scene.gltf");
            File.WriteAllText(gltfPath, json);
            File.WriteAllBytes(Path.Combine(tempDir.FullName, "scene.bin"), bin);

            var glb = GltfFilePacker.PackToGlb(gltfPath, maxBytes: 1024 * 1024);
            var scene = GltfBinaryReader.ReadScene(glb);

            scene.Meshes.Should().ContainSingle();
            scene.Meshes[0].Primitives[0].Geometry.Name.Should().Be("triangle");
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [Fact]
    public void PackToGlb_throws_when_the_sidecar_pushes_the_pack_over_budget()
    {
        var tempDir = Directory.CreateTempSubdirectory("opus-content-pack-overbudget-");
        try
        {
            var (json, bin) = GltfTestAssets.SplitTriangleScene();
            var gltfPath = Path.Combine(tempDir.FullName, "scene.gltf");
            File.WriteAllText(gltfPath, json);
            File.WriteAllBytes(Path.Combine(tempDir.FullName, "scene.bin"), bin);

            // A budget that exactly covers the glTF JSON leaves nothing for the sidecar buffer,
            // so the unbounded sidecar read is refused before any allocation.
            var gltfLength = new FileInfo(gltfPath).Length;

            var act = () => GltfFilePacker.PackToGlb(gltfPath, gltfLength);

            act.Should().Throw<GltfPackBudgetExceededException>()
                .Where(e => e.BudgetBytes == gltfLength && e.RequiredBytes == gltfLength + bin.Length);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void PackToGlb_rejects_a_non_positive_budget(long budget)
    {
        var tempDir = Directory.CreateTempSubdirectory("opus-content-pack-badbudget-");
        try
        {
            var (json, bin) = GltfTestAssets.SplitTriangleScene();
            var gltfPath = Path.Combine(tempDir.FullName, "scene.gltf");
            File.WriteAllText(gltfPath, json);
            File.WriteAllBytes(Path.Combine(tempDir.FullName, "scene.bin"), bin);

            var act = () => GltfFilePacker.PackToGlb(gltfPath, budget);

            act.Should().Throw<ArgumentOutOfRangeException>();
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }
}
