using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace Opus.Content.Tests.Fixtures;

internal static class GltfTestAssets
{
    public static (string Json, byte[] Bin) SplitTriangleScene()
    {
        var bin = BuildTriangleBin(includeImages: false);
        var json = $$"""
            {
              "asset": { "version": "2.0" },
              "buffers": [{ "uri": "scene.bin", "byteLength": {{bin.Length}} }],
              "bufferViews": [
                { "buffer": 0, "byteOffset": 0, "byteLength": 36 },
                { "buffer": 0, "byteOffset": 36, "byteLength": 36 },
                { "buffer": 0, "byteOffset": 72, "byteLength": 24 },
                { "buffer": 0, "byteOffset": 96, "byteLength": 6 }
              ],
              "accessors": [
                { "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3" },
                { "bufferView": 1, "componentType": 5126, "count": 3, "type": "VEC3" },
                { "bufferView": 2, "componentType": 5126, "count": 3, "type": "VEC2" },
                { "bufferView": 3, "componentType": 5123, "count": 3, "type": "SCALAR" }
              ],
              "meshes": [{
                "name": "triangle",
                "primitives": [{
                  "attributes": { "POSITION": 0, "NORMAL": 1, "TEXCOORD_0": 2 },
                  "indices": 3,
                  "material": 0
                }]
              }],
              "nodes": [
                { "name": "root", "children": [1], "translation": [10, 0, 0] },
                { "name": "gun", "mesh": 0, "translation": [0, 5, 0] }
              ],
              "scenes": [{ "name": "main", "nodes": [0] }],
              "scene": 0,
              "materials": [{ "name": "paint" }]
            }
            """;

        return (json, bin);
    }

    public static byte[] MaterialImageGlb()
    {
        var bin = BuildTriangleBin(includeImages: true);
        var json = $$"""
            {
              "asset": { "version": "2.0" },
              "buffers": [{ "byteLength": {{bin.Length}} }],
              "bufferViews": [
                { "buffer": 0, "byteOffset": 0, "byteLength": 36 },
                { "buffer": 0, "byteOffset": 36, "byteLength": 36 },
                { "buffer": 0, "byteOffset": 72, "byteLength": 24 },
                { "buffer": 0, "byteOffset": 96, "byteLength": 6 },
                { "buffer": 0, "byteOffset": 104, "byteLength": 4 },
                { "buffer": 0, "byteOffset": 108, "byteLength": 5 }
              ],
              "accessors": [
                { "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3" },
                { "bufferView": 1, "componentType": 5126, "count": 3, "type": "VEC3" },
                { "bufferView": 2, "componentType": 5126, "count": 3, "type": "VEC2" },
                { "bufferView": 3, "componentType": 5123, "count": 3, "type": "SCALAR" }
              ],
              "images": [
                { "uri": "external.png" },
                { "mimeType": "image/png", "bufferView": 4 },
                { "mimeType": "image/jpeg", "bufferView": 5 }
              ],
              "textures": [{ "source": 1 }, { "source": 2 }],
              "materials": [
                {
                  "name": "core",
                  "pbrMetallicRoughness": {
                    "baseColorTexture": { "index": 0 },
                    "baseColorFactor": [0.25, 0.5, 0.75, 1.0]
                  }
                },
                {
                  "name": "legacy",
                  "extensions": {
                    "KHR_materials_pbrSpecularGlossiness": {
                      "diffuseTexture": { "index": 1 },
                      "diffuseFactor": [0.1, 0.2, 0.3, 0.4]
                    }
                  }
                },
                {
                  "name": "flat",
                  "pbrMetallicRoughness": { "baseColorFactor": [1, 0, 0, 1] }
                }
              ],
              "meshes": [{
                "name": "triangle",
                "primitives": [{ "attributes": { "POSITION": 0 }, "indices": 3, "material": 0 }]
              }]
            }
            """;

        return PackGlb(Encoding.UTF8.GetBytes(json), bin);
    }

    /// <summary>A GLB whose single material binds the full metal-roughness map set — base
    /// colour, normal, metallic-roughness, occlusion and emissive — plus non-default scalar
    /// factors, so the reader's PBR-map resolution can be asserted end to end. One byte per
    /// map keeps the BIN minimal: binding resolution maps texture → image by index and never
    /// reads the image bytes (byte extraction is covered by the embedded-image tests).</summary>
    public static byte[] FullPbrMaterialGlb()
    {
        var bin = new byte[] { 10, 20, 30, 40, 50 };
        var json = $$"""
            {
              "asset": { "version": "2.0" },
              "buffers": [{ "byteLength": {{bin.Length}} }],
              "bufferViews": [
                { "buffer": 0, "byteOffset": 0, "byteLength": 1 },
                { "buffer": 0, "byteOffset": 1, "byteLength": 1 },
                { "buffer": 0, "byteOffset": 2, "byteLength": 1 },
                { "buffer": 0, "byteOffset": 3, "byteLength": 1 },
                { "buffer": 0, "byteOffset": 4, "byteLength": 1 }
              ],
              "images": [
                { "mimeType": "image/png", "bufferView": 0 },
                { "mimeType": "image/png", "bufferView": 1 },
                { "mimeType": "image/png", "bufferView": 2 },
                { "mimeType": "image/png", "bufferView": 3 },
                { "mimeType": "image/png", "bufferView": 4 }
              ],
              "textures": [
                { "source": 0 }, { "source": 1 }, { "source": 2 }, { "source": 3 }, { "source": 4 }
              ],
              "materials": [{
                "name": "pbr",
                "pbrMetallicRoughness": {
                  "baseColorTexture": { "index": 0 },
                  "metallicRoughnessTexture": { "index": 2 },
                  "metallicFactor": 0.3,
                  "roughnessFactor": 0.7
                },
                "normalTexture": { "index": 1 },
                "occlusionTexture": { "index": 3 },
                "emissiveTexture": { "index": 4 },
                "emissiveFactor": [0.6, 0.5, 0.4]
              }]
            }
            """;

        return PackGlb(Encoding.UTF8.GetBytes(json), bin);
    }

    public static byte[] TruncatedJsonChunkGlb()
    {
        var glb = new byte[20];
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(0, 4), 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(4, 4), 2u);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(8, 4), (uint)glb.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(12, 4), 128u);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(16, 4), 0x4E4F534A);
        return glb;
    }

    public static byte[] PackGlb(byte[] jsonBytes, byte[] binBytes)
    {
        var paddedJsonLength = AlignUp(jsonBytes.Length, 4);
        var paddedBinLength = AlignUp(binBytes.Length, 4);
        var totalLength = 12 + 8 + paddedJsonLength + 8 + paddedBinLength;
        var glb = new byte[totalLength];
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(0, 4), 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(4, 4), 2u);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(8, 4), (uint)totalLength);
        WriteChunk(glb, 12, 0x4E4F534A, jsonBytes, paddedJsonLength, 0x20);
        WriteChunk(glb, 20 + paddedJsonLength, 0x004E4942, binBytes, paddedBinLength, 0x00);
        return glb;
    }

    private static byte[] BuildTriangleBin(bool includeImages)
    {
        var bytes = new List<byte>(includeImages ? 116 : 104);
        AppendVec3(bytes, new Vector3(0f, 0f, 0f));
        AppendVec3(bytes, new Vector3(1f, 0f, 0f));
        AppendVec3(bytes, new Vector3(0f, 1f, 0f));
        AppendVec3(bytes, Vector3.UnitZ);
        AppendVec3(bytes, Vector3.UnitZ);
        AppendVec3(bytes, Vector3.UnitZ);
        AppendVec2(bytes, Vector2.Zero);
        AppendVec2(bytes, Vector2.UnitX);
        AppendVec2(bytes, Vector2.UnitY);
        AppendUshort(bytes, 0);
        AppendUshort(bytes, 1);
        AppendUshort(bytes, 2);
        bytes.Add(0);
        bytes.Add(0);
        if (includeImages)
        {
            bytes.AddRange(new byte[] { 1, 2, 3, 4 });
            bytes.AddRange(new byte[] { 5, 6, 7, 8, 9 });
        }

        return bytes.ToArray();
    }

    private static void AppendVec3(List<byte> bytes, Vector3 value)
    {
        AppendFloat(bytes, value.X);
        AppendFloat(bytes, value.Y);
        AppendFloat(bytes, value.Z);
    }

    private static void AppendVec2(List<byte> bytes, Vector2 value)
    {
        AppendFloat(bytes, value.X);
        AppendFloat(bytes, value.Y);
    }

    private static void AppendFloat(List<byte> bytes, float value)
    {
        Span<byte> scratch = stackalloc byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(scratch, value);
        bytes.AddRange(scratch.ToArray());
    }

    private static void AppendUshort(List<byte> bytes, ushort value)
    {
        Span<byte> scratch = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(scratch, value);
        bytes.AddRange(scratch.ToArray());
    }

    private static void WriteChunk(
        byte[] glb,
        int offset,
        uint chunkType,
        byte[] payload,
        int paddedLength,
        byte padByte)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(offset, 4), (uint)paddedLength);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(offset + 4, 4), chunkType);
        Array.Copy(payload, 0, glb, offset + 8, payload.Length);
        Array.Fill(glb, padByte, offset + 8 + payload.Length, paddedLength - payload.Length);
    }

    private static int AlignUp(int value, int alignment) =>
        (value + alignment - 1) & ~(alignment - 1);
}
