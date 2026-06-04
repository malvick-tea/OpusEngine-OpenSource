using System.Numerics;
using FluentAssertions;
using Opus.Content.Meshes;
using Opus.Content.Tests.Fixtures;
using Xunit;

namespace Opus.Content.Tests.Meshes;

public sealed class GltfImageReaderTests
{
    [Fact]
    public void ReadEmbeddedImagesByIndex_preserves_gltf_image_indices_when_external_images_exist()
    {
        var glb = GltfTestAssets.MaterialImageGlb();

        var images = GltfImageReader.ReadEmbeddedImagesByIndex(glb);

        images.Keys.Should().Equal(1, 2);
        images[1].MimeType.Should().Be("image/png");
        images[1].Bytes.Should().Equal((byte)1, (byte)2, (byte)3, (byte)4);
        images[2].MimeType.Should().Be("image/jpeg");
        images[2].Bytes.Should().Equal((byte)5, (byte)6, (byte)7, (byte)8, (byte)9);
    }

    [Fact]
    public void ReadMaterialBindings_resolves_core_spec_gloss_and_flat_materials()
    {
        var glb = GltfTestAssets.MaterialImageGlb();

        var bindings = GltfImageReader.ReadMaterialBindings(glb);

        bindings.Should().HaveCount(3);
        bindings[0].Name.Should().Be("core");
        bindings[0].BaseColorImageIndex.Should().Be(1);
        bindings[0].BaseColorFactor.X.Should().BeApproximately(0.25f, 1e-5f);
        bindings[0].BaseColorFactor.Y.Should().BeApproximately(0.5f, 1e-5f);
        bindings[0].BaseColorFactor.Z.Should().BeApproximately(0.75f, 1e-5f);
        bindings[1].Name.Should().Be("legacy");
        bindings[1].BaseColorImageIndex.Should().Be(2);
        bindings[1].BaseColorFactor.X.Should().BeApproximately(0.1f, 1e-5f);
        bindings[1].BaseColorFactor.W.Should().BeApproximately(0.4f, 1e-5f);
        bindings[2].Name.Should().Be("flat");
        bindings[2].BaseColorImageIndex.Should().BeNull();
        bindings[2].BaseColorFactor.X.Should().BeApproximately(1f, 1e-5f);

        // Materials that bind no PBR maps fall back to neutral defaults so the renderer can
        // substitute a flat normal / unit occlusion without branching per material.
        bindings[0].NormalImageIndex.Should().BeNull();
        bindings[0].MetallicRoughnessImageIndex.Should().BeNull();
        bindings[0].OcclusionImageIndex.Should().BeNull();
        bindings[0].EmissiveImageIndex.Should().BeNull();
        bindings[0].MetallicFactor.Should().BeApproximately(1f, 1e-5f);
        bindings[0].RoughnessFactor.Should().BeApproximately(1f, 1e-5f);
        bindings[0].EmissiveFactor.Should().Be(Vector3.Zero);

        // Spec-gloss exports carry no metalness — the reader treats them as dielectrics.
        bindings[1].MetallicFactor.Should().BeApproximately(0f, 1e-5f);
        bindings[1].RoughnessFactor.Should().BeApproximately(1f, 1e-5f);
    }

    [Fact]
    public void ReadMaterialBindings_resolves_full_metal_roughness_map_set()
    {
        var glb = GltfTestAssets.FullPbrMaterialGlb();

        var binding = GltfImageReader.ReadMaterialBindings(glb).Should().ContainSingle().Subject;

        binding.Name.Should().Be("pbr");
        binding.BaseColorImageIndex.Should().Be(0);
        binding.NormalImageIndex.Should().Be(1);
        binding.MetallicRoughnessImageIndex.Should().Be(2);
        binding.OcclusionImageIndex.Should().Be(3);
        binding.EmissiveImageIndex.Should().Be(4);
        binding.MetallicFactor.Should().BeApproximately(0.3f, 1e-5f);
        binding.RoughnessFactor.Should().BeApproximately(0.7f, 1e-5f);
        binding.EmissiveFactor.X.Should().BeApproximately(0.6f, 1e-5f);
        binding.EmissiveFactor.Y.Should().BeApproximately(0.5f, 1e-5f);
        binding.EmissiveFactor.Z.Should().BeApproximately(0.4f, 1e-5f);
    }
}
