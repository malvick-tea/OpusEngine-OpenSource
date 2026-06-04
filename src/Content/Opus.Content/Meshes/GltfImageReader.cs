using System;
using System.IO;
using System.Numerics;

namespace Opus.Content.Meshes;

/// <summary>A still-encoded image (PNG/JPEG bytes) extracted from a glTF binary, with
/// the MIME type the file declared. Caller decodes via <c>ImageDecoder.DecodeRgba8</c>
/// to get raw pixels.</summary>
public sealed record GltfTextureBlob(string MimeType, byte[] Bytes);

/// <summary>Per-material binding resolved from a glTF file: the embedded-image declaration
/// index (glTF <c>images[]</c>) for each PBR map the material references, plus the scalar
/// factors. An image index is <c>null</c> when the material omits that map; callers fall back
/// to the factor (base colour / metallic / roughness / emissive) or a neutral default (flat
/// normal, unit occlusion). Base colour also resolves the legacy
/// <c>KHR_materials_pbrSpecularGlossiness.diffuseTexture</c> for older exports.</summary>
public readonly record struct GltfMaterialBinding(string Name, int? BaseColorImageIndex, Vector4 BaseColorFactor)
{
    /// <summary>Tangent-space normal map image index (glTF <c>material.normalTexture</c>).</summary>
    public int? NormalImageIndex { get; init; }

    /// <summary>Metallic-roughness map image index
    /// (glTF <c>pbrMetallicRoughness.metallicRoughnessTexture</c>: G = roughness, B = metallic).</summary>
    public int? MetallicRoughnessImageIndex { get; init; }

    /// <summary>Ambient-occlusion image index (glTF <c>material.occlusionTexture</c>, R channel).</summary>
    public int? OcclusionImageIndex { get; init; }

    /// <summary>Emissive map image index (glTF <c>material.emissiveTexture</c>).</summary>
    public int? EmissiveImageIndex { get; init; }

    /// <summary>Scalar metalness multiplier (glTF default 1).</summary>
    public float MetallicFactor { get; init; }

    /// <summary>Scalar roughness multiplier (glTF default 1).</summary>
    public float RoughnessFactor { get; init; }

    /// <summary>Linear emissive colour multiplier (glTF default black = not emissive).</summary>
    public Vector3 EmissiveFactor { get; init; }
}

/// <summary>
/// Extracts the embedded PBR metallic-roughness <c>baseColorTexture</c> image bytes
/// from a glTF 2.0 binary. R-19.b uses just the first material's baseColor — full
/// per-material image catalogue follows in R-19.c.
/// </summary>
/// <remarks>
/// Only handles embedded textures (image references a <c>bufferView</c> inside the
/// GLB's BIN chunk). External-URI textures (<c>image.uri = "tank.png"</c>) return
/// null — those need a sibling-file loader the engine doesn't have yet.
/// </remarks>
public static class GltfImageReader
{
    public static GltfTextureBlob? TryReadFirstBaseColorImage(ReadOnlySpan<byte> glb)
    {
        var (doc, bin) = GlbChunkParser.Parse(glb);

        var materials = doc.Materials;
        if (materials is null || materials.Count == 0)
        {
            return null;
        }

        var textures = doc.Textures;
        if (textures is null || textures.Count == 0)
        {
            return null;
        }

        var images = doc.Images;
        if (images is null || images.Count == 0)
        {
            return null;
        }

        var bufferViews = doc.BufferViews;
        if (bufferViews is null || bufferViews.Count == 0)
        {
            return null;
        }

        foreach (var material in materials)
        {
            var texRef = material.PbrMetallicRoughness?.BaseColorTexture;
            if (texRef is null)
            {
                continue;
            }

            if (texRef.Index < 0 || texRef.Index >= textures.Count)
            {
                continue;
            }

            var texture = textures[texRef.Index];
            if (texture.Source is not int srcIdx)
            {
                continue;
            }

            if (srcIdx < 0 || srcIdx >= images.Count)
            {
                continue;
            }

            var image = images[srcIdx];
            if (image.BufferView is not int bvIdx)
            {
                continue;
            }

            if (bvIdx < 0 || bvIdx >= bufferViews.Count)
            {
                continue;
            }

            var bv = bufferViews[bvIdx];
            if (bv.ByteOffset < 0 || bv.ByteLength <= 0 || bv.ByteOffset + bv.ByteLength > bin.Length)
            {
                throw new InvalidDataException(
                    $"Image bufferView {bvIdx} out of range: offset={bv.ByteOffset}, length={bv.ByteLength}, bin={bin.Length}.");
            }

            var bytes = new byte[bv.ByteLength];
            Array.Copy(bin, bv.ByteOffset, bytes, 0, bv.ByteLength);
            return new GltfTextureBlob(image.MimeType ?? "image/octet-stream", bytes);
        }

        return null;
    }

    /// <summary>Fallback for legacy / non-PBR glTF assets that have embedded image buffers
    /// but no <c>materials[].pbrMetallicRoughness.baseColorTexture</c> chain (Godot exports,
    /// glTF 1.0-style materials, KHR_materials_pbrSpecularGlossiness exports, etc.). Returns
    /// the first <see cref="GltfImage"/> that references an embedded <c>bufferView</c> —
    /// usually the hull-camo texture for vehicle assets like the legacy a sample vehicle.</summary>
    public static GltfTextureBlob? TryReadFirstEmbeddedImage(ReadOnlySpan<byte> glb)
    {
        var blobs = ReadAllEmbeddedImages(glb);
        return blobs.Count == 0 ? null : blobs[0];
    }

    /// <summary>Returns every embedded image (one that references a <c>bufferView</c> in the
    /// GLB's BIN chunk), preserving glTF declaration order. External-URI images are skipped
    /// — they require a sibling-file loader. Order matters: callers like R-19.c map mesh →
    /// texture by index and rely on a stable enumeration.</summary>
    public static IReadOnlyList<GltfTextureBlob> ReadAllEmbeddedImages(ReadOnlySpan<byte> glb)
    {
        var (doc, bin) = GlbChunkParser.Parse(glb);
        var images = doc.Images;
        var bufferViews = doc.BufferViews;
        if (images is null || bufferViews is null)
        {
            return Array.Empty<GltfTextureBlob>();
        }

        var blobs = new List<GltfTextureBlob>(images.Count);
        foreach (var image in images)
        {
            var blob = TryReadEmbeddedImage(image, bufferViews, bin);
            if (blob is not null)
            {
                blobs.Add(blob);
            }
        }

        return blobs;
    }

    /// <summary>Same data as <see cref="ReadAllEmbeddedImages"/>, but keyed by the glTF
    /// <c>images[]</c> declaration index — so a material's <c>BaseColorImageIndex</c> can
    /// resolve to its blob in one lookup regardless of how many external-URI siblings
    /// preceded it in the document. External-URI / malformed entries are absent from the
    /// dictionary (rather than returning null) so the lookup is a single
    /// <see cref="IReadOnlyDictionary{TKey,TValue}.TryGetValue"/>. Used by the
    /// multi-material atlas builder, which would otherwise mis-index when a future
    /// glTF mixes embedded + external image sources.</summary>
    public static IReadOnlyDictionary<int, GltfTextureBlob> ReadEmbeddedImagesByIndex(ReadOnlySpan<byte> glb)
    {
        var (doc, bin) = GlbChunkParser.Parse(glb);
        var images = doc.Images;
        var bufferViews = doc.BufferViews;
        if (images is null || bufferViews is null)
        {
            return EmptyImageDictionary;
        }

        var byIndex = new Dictionary<int, GltfTextureBlob>(images.Count);
        for (var i = 0; i < images.Count; i++)
        {
            var blob = TryReadEmbeddedImage(images[i], bufferViews, bin);
            if (blob is not null)
            {
                byIndex[i] = blob;
            }
        }

        return byIndex;
    }

    private static GltfTextureBlob? TryReadEmbeddedImage(
        GltfImage image,
        IReadOnlyList<GltfBufferView> bufferViews,
        byte[] bin)
    {
        if (image.BufferView is not int bvIdx)
        {
            return null;
        }

        if (bvIdx < 0 || bvIdx >= bufferViews.Count)
        {
            return null;
        }

        var bv = bufferViews[bvIdx];
        if (bv.ByteOffset < 0 || bv.ByteLength <= 0 || bv.ByteOffset + bv.ByteLength > bin.Length)
        {
            return null;
        }

        var bytes = new byte[bv.ByteLength];
        Array.Copy(bin, bv.ByteOffset, bytes, 0, bv.ByteLength);
        return new GltfTextureBlob(image.MimeType ?? "image/octet-stream", bytes);
    }

    private static readonly IReadOnlyDictionary<int, GltfTextureBlob> EmptyImageDictionary =
        new Dictionary<int, GltfTextureBlob>();

    /// <summary>Resolves the base-colour binding for every material in the glTF file.
    /// Each material is inspected in this order:
    /// <list type="number">
    /// <item>glTF 2.0 core <c>pbrMetallicRoughness.baseColorTexture</c> + <c>baseColorFactor</c>.</item>
    /// <item>Legacy <c>KHR_materials_pbrSpecularGlossiness.diffuseTexture</c> + <c>diffuseFactor</c>
    /// (used by Godot 3.x exports — the entire sample-vehicle catalogue we ship was authored this way).</item>
    /// </list>
    /// The returned binding holds the glTF <c>images[]</c> declaration index,
    /// not the texture index — callers upload images once and bind by material → image lookup.
    /// Returns one entry per material, in declaration order. Empty when the file has no
    /// materials block. Materials with no texture binding return <c>BaseColorImageIndex == null</c>
    /// and the factor still propagates so the renderer can fall back to a flat colour.</summary>
    public static IReadOnlyList<GltfMaterialBinding> ReadMaterialBindings(ReadOnlySpan<byte> glb)
    {
        var (doc, _) = GlbChunkParser.Parse(glb);

        var materials = doc.Materials;
        if (materials is null || materials.Count == 0)
        {
            return Array.Empty<GltfMaterialBinding>();
        }

        var textures = doc.Textures;
        var images = doc.Images;

        var bindings = new GltfMaterialBinding[materials.Count];
        for (var i = 0; i < materials.Count; i++)
        {
            bindings[i] = ResolveBinding(materials[i], textures, images, fallbackName: $"material_{i}");
        }

        return bindings;
    }

    private static GltfMaterialBinding ResolveBinding(
        GltfMaterial material,
        IReadOnlyList<GltfTexture>? textures,
        IReadOnlyList<GltfImage>? images,
        string fallbackName)
    {
        var name = material.Name ?? fallbackName;

        // Maps declared on the material itself (not inside the metallic-roughness block) are
        // shared by both the core-PBR and the legacy spec-gloss base-colour branches.
        var normal = ResolveTextureImage(material.NormalTexture, textures, images);
        var occlusion = ResolveTextureImage(material.OcclusionTexture, textures, images);
        var emissive = ResolveTextureImage(material.EmissiveTexture, textures, images);
        var emissiveFactor = Vector3Factor(material.EmissiveFactor);

        // glTF 2.0 says pbrMetallicRoughness is the canonical PBR branch; the extensions
        // table is consulted only when the canonical branch is absent. sample-vehicle legacy
        // Godot exports omit pbrMetallicRoughness entirely and rely on the spec-gloss
        // extension — the fallback below handles that case without conflicting on assets
        // that declare both.
        var pbr = material.PbrMetallicRoughness;
        if (pbr is not null)
        {
            return new GltfMaterialBinding(name, ResolveTextureImage(pbr.BaseColorTexture, textures, images), FactorOrOne(pbr.BaseColorFactor))
            {
                NormalImageIndex = normal,
                MetallicRoughnessImageIndex = ResolveTextureImage(pbr.MetallicRoughnessTexture, textures, images),
                OcclusionImageIndex = occlusion,
                EmissiveImageIndex = emissive,
                MetallicFactor = pbr.MetallicFactor ?? 1f,
                RoughnessFactor = pbr.RoughnessFactor ?? 1f,
                EmissiveFactor = emissiveFactor,
            };
        }

        var sg = material.Extensions?.PbrSpecularGlossiness;
        if (sg is not null)
        {
            // Spec-gloss carries no metalness; treat the surface as a dielectric with an
            // unknown matte gloss. The PBR renderer relies on the authored ORM map for the
            // real values — these factors only apply when no map is bound.
            return new GltfMaterialBinding(name, ResolveTextureImage(sg.DiffuseTexture, textures, images), FactorOrOne(sg.DiffuseFactor))
            {
                NormalImageIndex = normal,
                OcclusionImageIndex = occlusion,
                EmissiveImageIndex = emissive,
                MetallicFactor = 0f,
                RoughnessFactor = 1f,
                EmissiveFactor = emissiveFactor,
            };
        }

        return new GltfMaterialBinding(name, BaseColorImageIndex: null, Vector4.One)
        {
            NormalImageIndex = normal,
            OcclusionImageIndex = occlusion,
            EmissiveImageIndex = emissive,
            MetallicFactor = 1f,
            RoughnessFactor = 1f,
            EmissiveFactor = emissiveFactor,
        };
    }

    private static int? ResolveTextureImage(
        GltfTextureRef? texRef,
        IReadOnlyList<GltfTexture>? textures,
        IReadOnlyList<GltfImage>? images)
    {
        if (texRef is null || textures is null || images is null)
        {
            return null;
        }

        if (texRef.Index < 0 || texRef.Index >= textures.Count)
        {
            return null;
        }

        var texture = textures[texRef.Index];
        if (texture.Source is not int srcIdx)
        {
            return null;
        }

        if (srcIdx < 0 || srcIdx >= images.Count)
        {
            return null;
        }

        // The image must be embedded (bufferView-backed) for the index to align with
        // ReadAllEmbeddedImages output. External-URI images are skipped there, so the
        // material's image index would silently misalign — surface that as "no binding"
        // rather than a wrong slot.
        return images[srcIdx].BufferView is not null ? srcIdx : null;
    }

    private static Vector4 FactorOrOne(float[]? factor)
        => factor is { Length: 4 } f ? new Vector4(f[0], f[1], f[2], f[3]) : Vector4.One;

    private static Vector3 Vector3Factor(float[]? factor)
        => factor is { Length: 3 } f ? new Vector3(f[0], f[1], f[2]) : Vector3.Zero;
}
