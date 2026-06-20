using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using FluentAssertions;
using Opus.Content.Meshes;
using Opus.Engine.Renderer.Direct3D12.Scene;
using Xunit;

namespace Opus.Engine.Direct3D12.Tests.Renderer;

/// <summary>Headless coverage for <see cref="MultiMaterialAtlasPlan"/> — the pure image-dedup +
/// per-material slot arithmetic the GPU <see cref="MultiMaterialAtlas"/> is built on. No device is
/// touched; the plan only manipulates glTF image indices + scalar factors. The invariant under
/// test: each distinct glTF image claims exactly one unique slot regardless of how many materials
/// or map kinds reference it.</summary>
public sealed class MultiMaterialAtlasPlanTests
{
    [Fact]
    public void Build_no_materials_yields_an_empty_layout()
    {
        var layout = MultiMaterialAtlasPlan.Build(new List<GltfMaterialBinding>());

        layout.UniqueImageIndices.Should().BeEmpty();
        layout.MaterialSlots.Should().BeEmpty();
    }

    [Fact]
    public void Build_dedups_a_base_image_shared_by_two_materials()
    {
        var bindings = new[]
        {
            Binding("a", baseColor: 7),
            Binding("b", baseColor: 7),
        };

        var layout = MultiMaterialAtlasPlan.Build(bindings);

        layout.UniqueImageIndices.Should().Equal(7);
        layout.MaterialSlots[0].UniqueImageSlot.Should().Be(0);
        layout.MaterialSlots[1].UniqueImageSlot.Should().Be(0);
    }

    [Fact]
    public void Build_resolves_the_full_map_set_to_distinct_slots_in_reference_order()
    {
        var bindings = new[]
        {
            Binding(
                "pbr",
                baseColor: 10,
                normal: 11,
                metallicRoughness: 12,
                occlusion: 13,
                emissive: 14,
                metallicFactor: 0.3f,
                roughnessFactor: 0.7f,
                emissive3: new Vector3(0.6f, 0.5f, 0.4f)),
        };

        var layout = MultiMaterialAtlasPlan.Build(bindings);

        layout.UniqueImageIndices.Should().Equal(10, 11, 12, 13, 14);
        var slot = layout.MaterialSlots[0];
        slot.UniqueImageSlot.Should().Be(0);
        slot.NormalSlot.Should().Be(1);
        slot.MetallicRoughnessSlot.Should().Be(2);
        slot.OcclusionSlot.Should().Be(3);
        slot.EmissiveSlot.Should().Be(4);
        slot.MetallicFactor.Should().Be(0.3f);
        slot.RoughnessFactor.Should().Be(0.7f);
        slot.EmissiveFactor.Should().Be(new Vector3(0.6f, 0.5f, 0.4f));
    }

    [Fact]
    public void Build_shares_one_slot_when_a_normal_map_reuses_another_materials_base_image()
    {
        var bindings = new[]
        {
            Binding("a", baseColor: 5),
            Binding("b", normal: 5),
        };

        var layout = MultiMaterialAtlasPlan.Build(bindings);

        layout.UniqueImageIndices.Should().Equal(5);
        layout.MaterialSlots[0].UniqueImageSlot.Should().Be(0);
        layout.MaterialSlots[1].NormalSlot.Should().Be(0);
        layout.MaterialSlots[1].UniqueImageSlot.Should().BeNull();
    }

    [Fact]
    public void Build_maps_an_untextured_material_to_null_slots_keeping_its_factors()
    {
        var bindings = new[]
        {
            Binding(
                "flat",
                baseColorFactor: new Vector4(0.2f, 0.4f, 0.6f, 1f),
                metallicFactor: 0f,
                roughnessFactor: 0.8f,
                emissive3: new Vector3(1f, 0f, 0f)),
        };

        var layout = MultiMaterialAtlasPlan.Build(bindings);

        layout.UniqueImageIndices.Should().BeEmpty();
        var slot = layout.MaterialSlots[0];
        slot.UniqueImageSlot.Should().BeNull();
        slot.NormalSlot.Should().BeNull();
        slot.MetallicRoughnessSlot.Should().BeNull();
        slot.OcclusionSlot.Should().BeNull();
        slot.EmissiveSlot.Should().BeNull();
        slot.Factor.Should().Be(new Vector4(0.2f, 0.4f, 0.6f, 1f));
        slot.MetallicFactor.Should().Be(0f);
        slot.RoughnessFactor.Should().Be(0.8f);
        slot.EmissiveFactor.Should().Be(new Vector3(1f, 0f, 0f));
    }

    [Fact]
    public void Build_rejects_more_than_the_material_limit()
    {
        var bindings = Enumerable.Range(0, MultiMaterialAtlasPlan.MaxMaterials + 1)
            .Select(i => Binding($"material-{i}"))
            .ToArray();

        var act = () => MultiMaterialAtlasPlan.Build(bindings);

        act.Should().Throw<InvalidDataException>().WithMessage("*material limit*");
    }

    [Fact]
    public void Build_rejects_more_than_the_unique_image_limit()
    {
        var bindings = Enumerable.Range(0, MultiMaterialAtlasPlan.MaxMaterials)
            .Select(i => Binding(
                $"material-{i}",
                baseColor: i * 3,
                normal: (i * 3) + 1,
                metallicRoughness: (i * 3) + 2))
            .ToArray();

        var act = () => MultiMaterialAtlasPlan.Build(bindings);

        act.Should().Throw<InvalidDataException>().WithMessage("*image limit*");
    }

    [Fact]
    public void Build_rejects_negative_image_indices()
    {
        var act = () => MultiMaterialAtlasPlan.Build(new[] { Binding("bad", baseColor: -1) });

        act.Should().Throw<InvalidDataException>().WithMessage("*non-negative*");
    }

    [Fact]
    public void Build_rejects_non_finite_material_factors()
    {
        var bindings = new[]
        {
            Binding(
                "unsafe",
                baseColorFactor: new Vector4(float.NaN, 1f, 1f, 1f)),
        };

        var act = () => MultiMaterialAtlasPlan.Build(bindings);

        act.Should().Throw<InvalidDataException>().WithMessage("*PBR factors*");
    }

    private static GltfMaterialBinding Binding(
        string name,
        int? baseColor = null,
        int? normal = null,
        int? metallicRoughness = null,
        int? occlusion = null,
        int? emissive = null,
        Vector4? baseColorFactor = null,
        float metallicFactor = 1f,
        float roughnessFactor = 1f,
        Vector3 emissive3 = default)
        => new(name, baseColor, baseColorFactor ?? Vector4.One)
        {
            NormalImageIndex = normal,
            MetallicRoughnessImageIndex = metallicRoughness,
            OcclusionImageIndex = occlusion,
            EmissiveImageIndex = emissive,
            MetallicFactor = metallicFactor,
            RoughnessFactor = roughnessFactor,
            EmissiveFactor = emissive3,
        };
}
