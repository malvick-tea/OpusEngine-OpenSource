using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Opus.Content.Meshes;

/// <summary>glTF 2.0 JSON DTOs for the binary reader. Only the subset Opus consumes is
/// modelled — POSITION/NORMAL/TANGENT/TEXCOORD_0 attributes, indices, the node graph + scenes.
/// Internal because callers receive the resolved <see cref="MeshData"/>/<see cref="GltfScene"/>,
/// not the raw JSON shape.</summary>
internal sealed class GltfDocument
{
    [JsonPropertyName("meshes")]
    public List<GltfMeshRaw>? Meshes { get; set; }

    [JsonPropertyName("accessors")]
    public List<GltfAccessor>? Accessors { get; set; }

    [JsonPropertyName("bufferViews")]
    public List<GltfBufferView>? BufferViews { get; set; }

    [JsonPropertyName("nodes")]
    public List<GltfNodeRaw>? Nodes { get; set; }

    [JsonPropertyName("scenes")]
    public List<GltfSceneRaw>? Scenes { get; set; }

    [JsonPropertyName("scene")]
    public int? Scene { get; set; }

    [JsonPropertyName("materials")]
    public List<GltfMaterial>? Materials { get; set; }

    [JsonPropertyName("textures")]
    public List<GltfTexture>? Textures { get; set; }

    [JsonPropertyName("images")]
    public List<GltfImage>? Images { get; set; }
}

internal sealed class GltfNodeRaw
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("children")]
    public List<int>? Children { get; set; }

    [JsonPropertyName("mesh")]
    public int? Mesh { get; set; }

    [JsonPropertyName("matrix")]
    public float[]? Matrix { get; set; }

    [JsonPropertyName("translation")]
    public float[]? Translation { get; set; }

    [JsonPropertyName("rotation")]
    public float[]? Rotation { get; set; }

    [JsonPropertyName("scale")]
    public float[]? Scale { get; set; }
}

internal sealed class GltfSceneRaw
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("nodes")]
    public List<int>? Nodes { get; set; }
}

internal sealed class GltfMeshRaw
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("primitives")]
    public List<GltfPrimitive>? Primitives { get; set; }
}

internal sealed class GltfPrimitive
{
    [JsonPropertyName("attributes")]
    public Dictionary<string, int>? Attributes { get; set; }

    [JsonPropertyName("indices")]
    public int? Indices { get; set; }

    [JsonPropertyName("material")]
    public int? Material { get; set; }
}

internal sealed class GltfMaterial
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("pbrMetallicRoughness")]
    public GltfPbrMetallicRoughness? PbrMetallicRoughness { get; set; }

    [JsonPropertyName("normalTexture")]
    public GltfTextureRef? NormalTexture { get; set; }

    [JsonPropertyName("occlusionTexture")]
    public GltfTextureRef? OcclusionTexture { get; set; }

    [JsonPropertyName("emissiveTexture")]
    public GltfTextureRef? EmissiveTexture { get; set; }

    [JsonPropertyName("emissiveFactor")]
    public float[]? EmissiveFactor { get; set; }

    [JsonPropertyName("extensions")]
    public GltfMaterialExtensions? Extensions { get; set; }
}

internal sealed class GltfMaterialExtensions
{
    /// <summary>The legacy <c>KHR_materials_pbrSpecularGlossiness</c> branch. Godot 3.x
    /// exports vehicles with this extension instead of glTF 2.0's core
    /// <c>pbrMetallicRoughness</c> chain; the engine treats the spec-gloss <c>diffuseTexture</c>
    /// as a fallback for base-colour resolution.</summary>
    [JsonPropertyName("KHR_materials_pbrSpecularGlossiness")]
    public GltfPbrSpecularGlossiness? PbrSpecularGlossiness { get; set; }
}

internal sealed class GltfPbrMetallicRoughness
{
    [JsonPropertyName("baseColorTexture")]
    public GltfTextureRef? BaseColorTexture { get; set; }

    [JsonPropertyName("baseColorFactor")]
    public float[]? BaseColorFactor { get; set; }

    [JsonPropertyName("metallicRoughnessTexture")]
    public GltfTextureRef? MetallicRoughnessTexture { get; set; }

    [JsonPropertyName("metallicFactor")]
    public float? MetallicFactor { get; set; }

    [JsonPropertyName("roughnessFactor")]
    public float? RoughnessFactor { get; set; }
}

internal sealed class GltfPbrSpecularGlossiness
{
    [JsonPropertyName("diffuseTexture")]
    public GltfTextureRef? DiffuseTexture { get; set; }

    [JsonPropertyName("diffuseFactor")]
    public float[]? DiffuseFactor { get; set; }

    [JsonPropertyName("specularGlossinessTexture")]
    public GltfTextureRef? SpecularGlossinessTexture { get; set; }

    [JsonPropertyName("specularFactor")]
    public float[]? SpecularFactor { get; set; }

    [JsonPropertyName("glossinessFactor")]
    public float? GlossinessFactor { get; set; }
}

internal sealed class GltfTextureRef
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("texCoord")]
    public int TexCoord { get; set; }
}

internal sealed class GltfTexture
{
    [JsonPropertyName("source")]
    public int? Source { get; set; }
}

internal sealed class GltfImage
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }

    [JsonPropertyName("bufferView")]
    public int? BufferView { get; set; }

    [JsonPropertyName("uri")]
    public string? Uri { get; set; }
}

internal sealed class GltfAccessor
{
    [JsonPropertyName("bufferView")]
    public int BufferView { get; set; }

    [JsonPropertyName("byteOffset")]
    public int ByteOffset { get; set; }

    [JsonPropertyName("componentType")]
    public int ComponentType { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "SCALAR";
}

internal sealed class GltfBufferView
{
    [JsonPropertyName("buffer")]
    public int Buffer { get; set; }

    [JsonPropertyName("byteOffset")]
    public int ByteOffset { get; set; }

    [JsonPropertyName("byteLength")]
    public int ByteLength { get; set; }
}
