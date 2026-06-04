using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Opus.Content.Meshes;

/// <summary>
/// Packs a Sketchfab-style <c>.gltf + .bin</c> pair into the GLB byte stream the rest
/// of the pipeline (<see cref="GltfBinaryReader"/>, <see cref="GltfImageReader"/>,
/// the D3D12 asset loader) consumes. Saves us from writing a parallel split-file
/// reader: the GLB path is already well-tested.
/// </summary>
/// <remarks>
/// <para>
/// glTF binary spec (<see href="https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html#glb-file-format-specification"/>):
/// 12-byte header (magic <c>glTF</c> + version + total length) → JSON chunk → optional
/// BIN chunk. Chunks are 4-byte aligned and padded with <c>0x20</c> (JSON) / <c>0x00</c>
/// (BIN). The first <c>buffers[0]</c> entry in the JSON must drop its <c>uri</c> field
/// in the packed form so the consumer reads bytes from the BIN chunk instead of an
/// external file.
/// </para>
/// <para>
/// External textures referenced by <c>uri</c> in <c>images[]</c> are left as-is — the
/// downstream <c>GltfImageReader</c> already returns <c>null</c> for non-embedded
/// images, which the renderer treats as "no texture, use the white-fallback atlas".
/// </para>
/// </remarks>
public static class GltfFilePacker
{
    private const uint GlbMagic = 0x46546C67;       // "glTF" little-endian
    private const uint ChunkTypeJson = 0x4E4F534A;  // "JSON"
    private const uint ChunkTypeBin = 0x004E4942;   // "BIN\0"
    private const int GlbHeaderSize = 12;
    private const int ChunkHeaderSize = 8;
    private const byte JsonPad = 0x20;
    private const byte BinPad = 0x00;
    private const int Alignment = 4;
    private const long NoBudget = long.MaxValue;

    /// <summary>Reads <paramref name="gltfPath"/> + its sidecar BIN (resolved from
    /// <c>buffers[0].uri</c>) and returns the packed GLB byte stream. Unbounded — for trusted
    /// runtime asset loading where the asset size is already known-good.</summary>
    public static byte[] PackToGlb(string gltfPath) => PackToGlb(gltfPath, NoBudget);

    /// <summary>Same as <see cref="PackToGlb(string)"/> but refuses to buffer more than
    /// <paramref name="maxBytes"/> in total (the glTF JSON file plus its sidecar buffers), throwing
    /// <see cref="GltfPackBudgetExceededException"/> instead. Use from a budget-bounded boundary
    /// (the package validator) so a small glTF pointing at a huge sidecar cannot drive an unbounded
    /// read.</summary>
    public static byte[] PackToGlb(string gltfPath, long maxBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gltfPath);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxBytes, 1);
        if (!File.Exists(gltfPath))
        {
            throw new FileNotFoundException($"glTF JSON not found: {gltfPath}", gltfPath);
        }

        var gltfLength = new FileInfo(gltfPath).Length;
        if (gltfLength > maxBytes)
        {
            throw new GltfPackBudgetExceededException(gltfLength, maxBytes);
        }

        var gltfText = File.ReadAllText(gltfPath);
        var dir = Path.GetDirectoryName(Path.GetFullPath(gltfPath))
                  ?? throw new InvalidDataException($"Cannot resolve directory for {gltfPath}.");
        var (rewrittenJson, binBytes) = ExtractBinaryBuffer(gltfText, dir, gltfLength, maxBytes);
        return PackBytes(Encoding.UTF8.GetBytes(rewrittenJson), binBytes);
    }

    private static (string Json, byte[] Bin) ExtractBinaryBuffer(
        string gltfJson, string baseDir, long gltfLength, long maxBytes)
    {
        var root = JsonNode.Parse(gltfJson)
            ?? throw new InvalidDataException("glTF JSON is empty.");
        var buffers = root["buffers"]?.AsArray()
            ?? throw new InvalidDataException("glTF has no buffers array.");
        if (buffers.Count == 0)
        {
            throw new InvalidDataException("glTF buffers array is empty.");
        }

        var buffer0 = buffers[0]
            ?? throw new InvalidDataException("glTF buffers[0] is null.");

        var uri = buffer0["uri"]?.GetValue<string>();
        if (string.IsNullOrEmpty(uri))
        {
            throw new InvalidDataException(
                "glTF buffers[0] has no uri — looks like an already-binary GLB; use GltfBinaryReader directly.");
        }

        var binPath = Path.Combine(baseDir, uri);
        if (!File.Exists(binPath))
        {
            throw new FileNotFoundException(
                $"glTF sidecar BIN not found at {binPath} (referenced by buffers[0].uri = \"{uri}\").",
                binPath);
        }

        // Check the sidecar size before reading: a small glTF can reference an arbitrarily large
        // BIN, so an unbounded File.ReadAllBytes here is the OOM vector a budget-bounded caller
        // (the package validator) must not pay.
        var requiredBytes = gltfLength + new FileInfo(binPath).Length;
        if (requiredBytes > maxBytes)
        {
            throw new GltfPackBudgetExceededException(requiredBytes, maxBytes);
        }

        var binBytes = File.ReadAllBytes(binPath);

        // Strip the uri so the downstream GLB parser reads from the BIN chunk.
        buffer0.AsObject().Remove("uri");
        var rewrittenJson = root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        return (rewrittenJson, binBytes);
    }

    private static byte[] PackBytes(byte[] jsonBytes, byte[] binBytes)
    {
        var paddedJsonLength = AlignUp(jsonBytes.Length, Alignment);
        var paddedBinLength = AlignUp(binBytes.Length, Alignment);
        var hasBin = binBytes.Length > 0;
        var totalLength = GlbHeaderSize + ChunkHeaderSize + paddedJsonLength;
        if (hasBin)
        {
            totalLength += ChunkHeaderSize + paddedBinLength;
        }

        var glb = new byte[totalLength];
        var offset = 0;
        offset = WriteHeader(glb, offset, (uint)totalLength);
        offset = WriteChunk(glb, offset, ChunkTypeJson, jsonBytes, paddedJsonLength, JsonPad);
        if (hasBin)
        {
            offset = WriteChunk(glb, offset, ChunkTypeBin, binBytes, paddedBinLength, BinPad);
        }

        if (offset != totalLength)
        {
            throw new InvalidOperationException(
                $"Packer math error: wrote {offset} bytes, expected {totalLength}.");
        }

        return glb;
    }

    private static int WriteHeader(byte[] glb, int offset, uint totalLength)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(offset, 4), GlbMagic);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(offset + 4, 4), 2u);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(offset + 8, 4), totalLength);
        return offset + GlbHeaderSize;
    }

    private static int WriteChunk(
        byte[] glb,
        int offset,
        uint chunkType,
        byte[] payload,
        int paddedLength,
        byte padByte)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(offset, 4), (uint)paddedLength);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(offset + 4, 4), chunkType);
        offset += ChunkHeaderSize;
        Array.Copy(payload, 0, glb, offset, payload.Length);
        for (var i = payload.Length; i < paddedLength; i++)
        {
            glb[offset + i] = padByte;
        }

        return offset + paddedLength;
    }

    private static int AlignUp(int value, int alignment) =>
        (value + alignment - 1) & ~(alignment - 1);
}
