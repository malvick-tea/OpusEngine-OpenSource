using System.Collections.Generic;
using System.Numerics;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>Per-material binding after image deduplication across the whole metal-roughness
/// map set. Every <c>*Slot</c> is an index into <see cref="MultiMaterialAtlasLayout.UniqueImageIndices"/>
/// (and so into the parallel <see cref="MultiMaterialAtlas"/> texture array), or <c>null</c>
/// when the material omits that map — in which case the renderer binds a neutral fallback
/// (white for base / metallic-roughness / occlusion, flat normal for the normal map) and
/// relies on the scalar factor. <see cref="UniqueImageSlot"/> is the base-colour slot;
/// <see cref="Factor"/> is its RGBA multiplier. Maps that reference the same glTF image —
/// even across different materials or different map kinds — share a single slot.</summary>
public readonly record struct MultiMaterialSlot(int? UniqueImageSlot, Vector4 Factor)
{
    /// <summary>Tangent-space normal-map slot; neutral fallback is a flat normal.</summary>
    public int? NormalSlot { get; init; }

    /// <summary>Metallic-roughness slot (G = roughness, B = metallic); neutral fallback is white.</summary>
    public int? MetallicRoughnessSlot { get; init; }

    /// <summary>Ambient-occlusion slot (R channel); neutral fallback is white (unoccluded).</summary>
    public int? OcclusionSlot { get; init; }

    /// <summary>Emissive-map slot; neutral fallback is white, so emission equals the factor.</summary>
    public int? EmissiveSlot { get; init; }

    /// <summary>Scalar metalness multiplier (glTF default 1).</summary>
    public float MetallicFactor { get; init; }

    /// <summary>Scalar roughness multiplier (glTF default 1).</summary>
    public float RoughnessFactor { get; init; }

    /// <summary>Linear emissive colour multiplier (glTF default black).</summary>
    public Vector3 EmissiveFactor { get; init; }
}

/// <summary>The pure-data shape <see cref="MultiMaterialAtlasPlan.Build"/> produces. Holds
/// the deduplicated list of glTF embedded-image indices the GPU layer must upload, plus
/// the per-material lookup that maps a glTF <c>materials[]</c> index to the unique-slot
/// the renderer binds when drawing that primitive.</summary>
public sealed record MultiMaterialAtlasLayout(
    IReadOnlyList<int> UniqueImageIndices,
    IReadOnlyList<MultiMaterialSlot> MaterialSlots);
