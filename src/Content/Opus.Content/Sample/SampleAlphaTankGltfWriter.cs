using System;
using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using System.Text;

namespace Opus.Content.Sample;

/// <summary>Procedural, self-contained GLB emitter that produces a single-triangle
/// "tank marker" asset usable by Opus alpha smoke tests and the sample host app when no
/// real consumer asset is provided. The output is a fully valid glTF 2.0 binary with
/// POSITION + NORMAL + TEXCOORD_0 attributes and a baseColorFactor material, so it
/// exercises the same loader path a real model would.
/// <para>
/// Lives in <see cref="Opus.Content.Sample"/> to make the intent obvious: this is
/// developer-facing sample-asset generation, not a consumer game asset. The engine
/// never bundles real game models (see <c>docs/engine-project-context.md</c> §5); a
/// consumer project supplies its own assets at integration time.
/// </para>
/// </summary>
public static class SampleAlphaTankGltfWriter
{
    private const uint GltfMagic = 0x46546C67;
    private const uint GltfVersion = 2u;
    private const uint ChunkTypeJson = 0x4E4F534A;
    private const uint ChunkTypeBin = 0x004E4942;
    private const byte JsonChunkPad = 0x20;
    private const byte BinChunkPad = 0x00;
    private const int ChunkAlignment = 4;

    private const int BinByteLength = 104;
    private const int PositionsByteOffset = 0;
    private const int NormalsByteOffset = 36;
    private const int TexcoordsByteOffset = 72;
    private const int IndicesByteOffset = 96;

    /// <summary>Builds the full GLB byte payload. Pure: no IO, no allocations beyond the
    /// returned arrays.</summary>
    public static byte[] BuildGlb()
    {
        var bin = BuildTriangleBin();
        var json = Encoding.UTF8.GetBytes(BuildGltfJson());
        return PackGlb(json, bin);
    }

    /// <summary>Writes the GLB to <paramref name="path"/>, creating intermediate
    /// directories. Overwrites any existing file at the path.</summary>
    public static void WriteTo(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(path, BuildGlb());
    }

    private static string BuildGltfJson() => $$"""
        {
          "asset": { "version": "2.0" },
          "buffers": [{ "byteLength": {{BinByteLength}} }],
          "bufferViews": [
            { "buffer": 0, "byteOffset": {{PositionsByteOffset}}, "byteLength": 36 },
            { "buffer": 0, "byteOffset": {{NormalsByteOffset}}, "byteLength": 36 },
            { "buffer": 0, "byteOffset": {{TexcoordsByteOffset}}, "byteLength": 24 },
            { "buffer": 0, "byteOffset": {{IndicesByteOffset}}, "byteLength": 6 }
          ],
          "accessors": [
            { "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3" },
            { "bufferView": 1, "componentType": 5126, "count": 3, "type": "VEC3" },
            { "bufferView": 2, "componentType": 5126, "count": 3, "type": "VEC2" },
            { "bufferView": 3, "componentType": 5123, "count": 3, "type": "SCALAR" }
          ],
          "materials": [{
            "name": "alpha-sample-paint",
            "pbrMetallicRoughness": { "baseColorFactor": [0.78, 0.88, 0.62, 1.0] }
          }],
          "meshes": [{
            "name": "alpha-sample-tank-marker",
            "primitives": [{
              "attributes": { "POSITION": 0, "NORMAL": 1, "TEXCOORD_0": 2 },
              "indices": 3,
              "material": 0
            }]
          }],
          "nodes": [{ "name": "tank-marker", "mesh": 0 }],
          "scenes": [{ "name": "main", "nodes": [0] }],
          "scene": 0
        }
        """;

    private static byte[] BuildTriangleBin()
    {
        var bytes = new byte[BinByteLength];
        var offset = 0;
        WriteVec3(bytes, ref offset, new Vector3(-0.6f, 0f, 0f));
        WriteVec3(bytes, ref offset, new Vector3(0.6f, 0f, 0f));
        WriteVec3(bytes, ref offset, new Vector3(0f, 0.9f, 0f));
        WriteVec3(bytes, ref offset, -Vector3.UnitZ);
        WriteVec3(bytes, ref offset, -Vector3.UnitZ);
        WriteVec3(bytes, ref offset, -Vector3.UnitZ);
        WriteVec2(bytes, ref offset, Vector2.Zero);
        WriteVec2(bytes, ref offset, Vector2.UnitX);
        WriteVec2(bytes, ref offset, Vector2.UnitY);
        WriteUshort(bytes, ref offset, 0);
        WriteUshort(bytes, ref offset, 1);
        WriteUshort(bytes, ref offset, 2);
        return bytes;
    }

    private static byte[] PackGlb(byte[] jsonBytes, byte[] binBytes)
    {
        var paddedJsonLength = AlignUp(jsonBytes.Length, ChunkAlignment);
        var paddedBinLength = AlignUp(binBytes.Length, ChunkAlignment);
        var totalLength = 12 + 8 + paddedJsonLength + 8 + paddedBinLength;
        var glb = new byte[totalLength];
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(0, 4), GltfMagic);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(4, 4), GltfVersion);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(8, 4), (uint)totalLength);
        WriteChunk(glb, 12, ChunkTypeJson, jsonBytes, paddedJsonLength, JsonChunkPad);
        WriteChunk(glb, 20 + paddedJsonLength, ChunkTypeBin, binBytes, paddedBinLength, BinChunkPad);
        return glb;
    }

    private static void WriteVec3(byte[] bytes, ref int offset, Vector3 value)
    {
        WriteFloat(bytes, ref offset, value.X);
        WriteFloat(bytes, ref offset, value.Y);
        WriteFloat(bytes, ref offset, value.Z);
    }

    private static void WriteVec2(byte[] bytes, ref int offset, Vector2 value)
    {
        WriteFloat(bytes, ref offset, value.X);
        WriteFloat(bytes, ref offset, value.Y);
    }

    private static void WriteFloat(byte[] bytes, ref int offset, float value)
    {
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset, 4), value);
        offset += 4;
    }

    private static void WriteUshort(byte[] bytes, ref int offset, ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, 2), value);
        offset += 2;
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
