using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Opus.Content.Meshes;

/// <summary>Shared helper that strips the 12-byte GLB header + JSON chunk + optional BIN
/// chunk out of a glTF 2.0 binary blob and deserialises the JSON into a
/// <see cref="GltfDocument"/>. Used by both <see cref="GltfBinaryReader"/> (mesh + scene
/// graph) and <see cref="GltfImageReader"/> (textures + materials + embedded images).
/// Centralising the chunk parser keeps the validation rules in one place.</summary>
internal static class GlbChunkParser
{
    private const uint GlbMagic = 0x46546C67;       // "glTF" little-endian
    private const uint ChunkTypeJson = 0x4E4F534A;  // "JSON"
    private const uint ChunkTypeBin = 0x004E4942;   // "BIN\0"

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static (GltfDocument Doc, byte[] Bin) Parse(ReadOnlySpan<byte> glb)
    {
        if (glb.Length < 12)
        {
            throw new InvalidDataException("GLB shorter than header.");
        }

        var magic = BinaryPrimitives.ReadUInt32LittleEndian(glb[..4]);
        if (magic != GlbMagic)
        {
            throw new InvalidDataException("Not a GLB file — magic mismatch.");
        }

        var version = BinaryPrimitives.ReadUInt32LittleEndian(glb.Slice(4, 4));
        if (version != 2)
        {
            throw new InvalidDataException($"Unsupported GLB version {version} (expected 2).");
        }

        var totalLength = BinaryPrimitives.ReadUInt32LittleEndian(glb.Slice(8, 4));
        if (totalLength != (uint)glb.Length)
        {
            throw new InvalidDataException($"GLB length mismatch: header says {totalLength}, span is {glb.Length}.");
        }

        var offset = 12;
        if (offset + 8 > glb.Length)
        {
            throw new InvalidDataException("GLB truncated before JSON chunk header.");
        }

        var jsonLength = ReadChunkLength(glb, offset, "JSON");
        var jsonType = BinaryPrimitives.ReadUInt32LittleEndian(glb.Slice(offset + 4, 4));
        if (jsonType != ChunkTypeJson)
        {
            throw new InvalidDataException("Expected JSON chunk after header.");
        }

        offset += 8;
        if (offset + jsonLength > glb.Length)
        {
            throw new InvalidDataException(
                $"GLB truncated inside JSON chunk: length={jsonLength}, remaining={glb.Length - offset}.");
        }

        var jsonBytes = glb.Slice(offset, jsonLength);
        var json = Encoding.UTF8.GetString(jsonBytes);
        offset += jsonLength;

        var bin = Array.Empty<byte>();
        if (offset + 8 <= glb.Length)
        {
            var binLength = ReadChunkLength(glb, offset, "BIN");
            var binType = BinaryPrimitives.ReadUInt32LittleEndian(glb.Slice(offset + 4, 4));
            if (binType != ChunkTypeBin)
            {
                throw new InvalidDataException("Second chunk is not BIN.");
            }

            offset += 8;
            if (offset + binLength > glb.Length)
            {
                throw new InvalidDataException(
                    $"GLB truncated inside BIN chunk: length={binLength}, remaining={glb.Length - offset}.");
            }

            bin = glb.Slice(offset, binLength).ToArray();
        }

        var doc = JsonSerializer.Deserialize<GltfDocument>(json, JsonOptions)
            ?? throw new InvalidDataException("GLB JSON chunk is empty.");

        return (doc, bin);
    }

    private static int ReadChunkLength(ReadOnlySpan<byte> glb, int offset, string chunkName)
    {
        var length = BinaryPrimitives.ReadUInt32LittleEndian(glb.Slice(offset, 4));
        if (length > int.MaxValue)
        {
            throw new InvalidDataException($"{chunkName} chunk is too large: {length} bytes.");
        }

        return (int)length;
    }
}
