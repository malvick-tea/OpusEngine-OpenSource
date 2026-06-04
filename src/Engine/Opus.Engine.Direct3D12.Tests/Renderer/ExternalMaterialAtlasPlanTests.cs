using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FluentAssertions;
using Opus.Content.Meshes;
using Opus.Engine.Renderer.Direct3D12.Scene;
using Xunit;

namespace Opus.Engine.Direct3D12.Tests.Renderer;

/// <summary>Headless coverage for <see cref="ExternalMaterialAtlasPlan"/> — the pure path +
/// slot arithmetic that resolves a material's PBR maps from loose files on disk (the runtime
/// path for 4K sets too large to embed; see <c>content/maps/japan/TEXTURE_SPEC.md</c>). No
/// device or real file system is touched: an injected <c>fileExists</c> probe stands in for
/// disk, seeded through <see cref="ExternalMaterialAtlasPlan.MapPath"/> so the test pins the
/// exact <c>{root}/{name}/{name}_{map}.png</c> convention the builder reads. The invariants
/// under test: the packed ORM file uploads once yet feeds both the metallic-roughness and
/// occlusion slots, and an absent file resolves to a null slot the GPU layer fills with a
/// neutral fallback.</summary>
public sealed class ExternalMaterialAtlasPlanTests
{
    private const string Root = "japan-textures-root";

    [Fact]
    public void Build_resolves_all_four_maps_and_feeds_the_packed_orm_to_both_pbr_slots()
    {
        var bindings = new[] { Binding("downtown_tower") };
        var fileExists = FilesPresent(
            ("downtown_tower", ExternalMaterialAtlasPlan.BaseColorMap),
            ("downtown_tower", ExternalMaterialAtlasPlan.NormalMap),
            ("downtown_tower", ExternalMaterialAtlasPlan.OrmMap),
            ("downtown_tower", ExternalMaterialAtlasPlan.EmissiveMap));

        var layout = ExternalMaterialAtlasPlan.Build(bindings, Root, fileExists);

        layout.UniqueImages.Should().HaveCount(4, "ORM is one file even though it feeds two slots");
        layout.UniqueImages.Select(image => image.Kind).Should().Equal(
            ExternalMaterialMapKind.BaseColor,
            ExternalMaterialMapKind.Normal,
            ExternalMaterialMapKind.Orm,
            ExternalMaterialMapKind.Emissive);
        var slot = layout.MaterialSlots[0];
        slot.UniqueImageSlot.Should().Be(0);
        slot.NormalSlot.Should().Be(1);
        slot.MetallicRoughnessSlot.Should().Be(2);
        slot.EmissiveSlot.Should().Be(3);
        slot.OcclusionSlot.Should().Be(
            slot.MetallicRoughnessSlot,
            "the packed ORM file uploads once and both the metallic-roughness and occlusion descriptors view it");
    }

    [Fact]
    public void Build_maps_absent_files_to_null_slots_keeping_the_present_one()
    {
        var bindings = new[] { Binding("machiya_wall") };
        var fileExists = FilesPresent(("machiya_wall", ExternalMaterialAtlasPlan.BaseColorMap));

        var layout = ExternalMaterialAtlasPlan.Build(bindings, Root, fileExists);

        layout.UniqueImages.Should().ContainSingle()
            .Which.Path.Should().Be(ExternalMaterialAtlasPlan.MapPath(Root, "machiya_wall", ExternalMaterialAtlasPlan.BaseColorMap));
        var slot = layout.MaterialSlots[0];
        slot.UniqueImageSlot.Should().Be(0);
        slot.NormalSlot.Should().BeNull();
        slot.MetallicRoughnessSlot.Should().BeNull();
        slot.OcclusionSlot.Should().BeNull();
        slot.EmissiveSlot.Should().BeNull();
    }

    [Fact]
    public void Build_propagates_base_colour_metallic_roughness_and_emissive_factors()
    {
        var bindings = new[]
        {
            Binding(
                "neon_sign",
                baseColorFactor: new Vector4(0.2f, 0.4f, 0.6f, 1f),
                metallicFactor: 0.1f,
                roughnessFactor: 0.85f,
                emissive: new Vector3(0.9f, 0.3f, 0.05f)),
        };

        var layout = ExternalMaterialAtlasPlan.Build(bindings, Root, NoFiles);

        var slot = layout.MaterialSlots[0];
        slot.Factor.Should().Be(new Vector4(0.2f, 0.4f, 0.6f, 1f));
        slot.MetallicFactor.Should().Be(0.1f);
        slot.RoughnessFactor.Should().Be(0.85f);
        slot.EmissiveFactor.Should().Be(new Vector3(0.9f, 0.3f, 0.05f));
    }

    [Fact]
    public void Build_maps_a_material_with_no_files_to_all_null_slots_and_no_images()
    {
        var bindings = new[] { Binding("park_grass") };

        var layout = ExternalMaterialAtlasPlan.Build(bindings, Root, NoFiles);

        layout.UniqueImages.Should().BeEmpty();
        var slot = layout.MaterialSlots[0];
        slot.UniqueImageSlot.Should().BeNull();
        slot.NormalSlot.Should().BeNull();
        slot.MetallicRoughnessSlot.Should().BeNull();
        slot.OcclusionSlot.Should().BeNull();
        slot.EmissiveSlot.Should().BeNull();
    }

    private static bool NoFiles(string path) => false;

    private static Func<string, bool> FilesPresent(params (string Material, string Map)[] present)
    {
        var existing = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (material, map) in present)
        {
            existing.Add(ExternalMaterialAtlasPlan.MapPath(Root, material, map));
        }

        return existing.Contains;
    }

    private static GltfMaterialBinding Binding(
        string name,
        Vector4? baseColorFactor = null,
        float metallicFactor = 1f,
        float roughnessFactor = 1f,
        Vector3 emissive = default)
        => new(name, BaseColorImageIndex: null, baseColorFactor ?? Vector4.One)
        {
            MetallicFactor = metallicFactor,
            RoughnessFactor = roughnessFactor,
            EmissiveFactor = emissive,
        };
}
