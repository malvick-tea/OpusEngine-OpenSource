using System;
using System.Collections.Generic;
using System.IO;
using Opus.Content.Meshes;
using Opus.Foundation.IO;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>Which authored PBR map an external image is. Selects both the on-disk filename token
/// and — through <see cref="ExternalTextureCompression"/> — the block-compression format the GPU
/// layer encodes it to (base colour + emissive → BC7 sRGB, the packed ORM → BC7 linear, normals →
/// BC5).</summary>
public enum ExternalMaterialMapKind
{
    BaseColor,
    Normal,
    Orm,
    Emissive,
}

/// <summary>One deduplicated on-disk image the GPU layer must load, block-compress, and upload,
/// tagged with the map kind that drives its compression + sampling format.</summary>
public sealed record ExternalMaterialImage(string Path, ExternalMaterialMapKind Kind);

/// <summary>The pure-data shape <see cref="ExternalMaterialAtlasPlan.Build"/> produces: the
/// deduplicated list of on-disk images the GPU layer must load + upload, plus the per-material slot
/// table that maps a glTF material to the unique-image slots the renderer binds. Mirrors
/// <see cref="MultiMaterialAtlasLayout"/> but keys on file paths instead of glTF embedded image
/// indices.</summary>
public sealed record ExternalMaterialAtlasLayout(
    IReadOnlyList<ExternalMaterialImage> UniqueImages,
    IReadOnlyList<MultiMaterialSlot> MaterialSlots);

/// <summary>Pure planning step for loading a material's PBR maps from loose image files on disk
/// rather than glTF-embedded images — the runtime path for 4K sets too large to embed (see
/// <c>content/maps/japan/TEXTURE_SPEC.md</c>). The on-disk convention is
/// <c>{root}/{materialName}/{materialName}_{map}.png</c> with <c>map</c> in
/// <c>basecolor / normal / orm / emissive</c>. The packed ORM file (R = AO, G = roughness,
/// B = metallic) feeds BOTH the metallic-roughness and occlusion shader slots, so it uploads once
/// and two descriptors view it. A missing file resolves to a <c>null</c> slot — the GPU layer
/// substitutes a neutral fallback. Behind a planning wall so the path + dedup arithmetic is
/// headless-testable through an injected file-exists probe.</summary>
public static class ExternalMaterialAtlasPlan
{
    public const string BaseColorMap = "basecolor";
    public const string NormalMap = "normal";
    public const string OrmMap = "orm";
    public const string EmissiveMap = "emissive";
    private const string MapExtension = ".png";

    public static ExternalMaterialAtlasLayout Build(
        IReadOnlyList<GltfMaterialBinding> materials,
        string texturesRoot,
        Func<string, bool> fileExists)
    {
        var pathToSlot = new Dictionary<string, int>(materials.Count, StringComparer.Ordinal);
        var uniqueImages = new List<ExternalMaterialImage>(materials.Count);
        var slots = new MultiMaterialSlot[materials.Count];

        int? SlotFor(string materialName, ExternalMaterialMapKind kind)
        {
            var path = MapPath(texturesRoot, materialName, MapToken(kind));
            if (!fileExists(path))
            {
                return null;
            }

            if (!pathToSlot.TryGetValue(path, out var existing))
            {
                existing = uniqueImages.Count;
                pathToSlot[path] = existing;
                uniqueImages.Add(new ExternalMaterialImage(path, kind));
            }

            return existing;
        }

        for (var m = 0; m < materials.Count; m++)
        {
            var binding = materials[m];
            var baseColor = SlotFor(binding.Name, ExternalMaterialMapKind.BaseColor);
            var normal = SlotFor(binding.Name, ExternalMaterialMapKind.Normal);
            var orm = SlotFor(binding.Name, ExternalMaterialMapKind.Orm);
            var emissive = SlotFor(binding.Name, ExternalMaterialMapKind.Emissive);

            slots[m] = new MultiMaterialSlot(baseColor, binding.BaseColorFactor)
            {
                NormalSlot = normal,
                MetallicRoughnessSlot = orm,
                OcclusionSlot = orm,
                EmissiveSlot = emissive,
                MetallicFactor = binding.MetallicFactor,
                RoughnessFactor = binding.RoughnessFactor,
                EmissiveFactor = binding.EmissiveFactor,
            };
        }

        return new ExternalMaterialAtlasLayout(uniqueImages, slots);
    }

    /// <summary>The on-disk path a material's map is expected at, per the
    /// <c>{root}/{materialName}/{materialName}_{map}.png</c> convention.</summary>
    public static string MapPath(string texturesRoot, string materialName, string map)
    {
        ValidateLeafToken(materialName, nameof(materialName));
        ValidateLeafToken(map, nameof(map));
        return PathContainment.ResolveUnderRoot(
            texturesRoot,
            Path.Combine(materialName, $"{materialName}_{map}{MapExtension}"));
    }

    private static string MapToken(ExternalMaterialMapKind kind) => kind switch
    {
        ExternalMaterialMapKind.BaseColor => BaseColorMap,
        ExternalMaterialMapKind.Normal => NormalMap,
        ExternalMaterialMapKind.Orm => OrmMap,
        ExternalMaterialMapKind.Emissive => EmissiveMap,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown external material map kind."),
    };

    private static void ValidateLeafToken(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value is "." or ".."
            || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || value.Contains(Path.DirectorySeparatorChar)
            || value.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("Material path tokens must be plain file-name segments.", parameterName);
        }
    }
}
