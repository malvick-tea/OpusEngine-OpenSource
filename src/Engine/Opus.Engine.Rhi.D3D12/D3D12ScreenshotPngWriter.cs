using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Writes a <see cref="D3D12Screenshot"/> out as a real PNG. The minimal IHDR
/// + IDAT + IEND chunk stream is followed by an optional run of tEXt chunks carrying
/// <see cref="D3D12ScreenshotMetadata"/> so the file is self-describing — a screenshot
/// emailed by a tester reveals the build, adapter, and frame index inline without a
/// separate metadata blob.
/// <para>
/// Implementation is dependency-free: <see cref="ZLibStream"/> from the BCL produces the
/// zlib-wrapped deflate body, a small ISO-3309 CRC32 table covers chunk integrity, and
/// scanlines use filter byte 0 (no filter) because alpha-grade captures favour minimal
/// CPU work over the marginal size win from row prediction. Output is a fully
/// spec-compliant 8-bit RGBA PNG readable by every common viewer.
/// </para>
/// </summary>
public static class D3D12ScreenshotPngWriter
{
    private const int BytesPerPixel = 4;
    private const byte BitDepth = 8;
    private const byte ColourTypeRgba = 6;
    private const byte FilterNone = 0;

    private static readonly byte[] PngSignature =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
    };

    private static readonly byte[] IhdrType = { (byte)'I', (byte)'H', (byte)'D', (byte)'R' };
    private static readonly byte[] IdatType = { (byte)'I', (byte)'D', (byte)'A', (byte)'T' };
    private static readonly byte[] IendType = { (byte)'I', (byte)'E', (byte)'N', (byte)'D' };
    private static readonly byte[] TextType = { (byte)'t', (byte)'E', (byte)'X', (byte)'t' };

    private static readonly uint[] CrcTable = BuildCrcTable();

    public static void Write(string path, D3D12Screenshot screenshot, D3D12ScreenshotMetadata metadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(screenshot);
        ArgumentNullException.ThrowIfNull(metadata);

        var expected = checked(screenshot.Width * screenshot.Height * BytesPerPixel);
        if (screenshot.Rgba8.Length != expected)
        {
            throw new ArgumentException(
                $"Screenshot RGBA payload is {screenshot.Rgba8.Length} bytes; expected {expected}.",
                nameof(screenshot));
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        fs.Write(PngSignature, 0, PngSignature.Length);
        WriteIhdr(fs, screenshot.Width, screenshot.Height);
        foreach (var entry in metadata.Entries)
        {
            WriteTextChunk(fs, entry.Keyword, entry.Value);
        }

        WriteIdat(fs, screenshot);
        WriteChunk(fs, IendType, ReadOnlySpan<byte>.Empty);
    }

    private static void WriteIhdr(Stream stream, int width, int height)
    {
        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr[..4], width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.Slice(4, 4), height);
        ihdr[8] = BitDepth;
        ihdr[9] = ColourTypeRgba;
        ihdr[10] = 0;
        ihdr[11] = 0;
        ihdr[12] = 0;
        WriteChunk(stream, IhdrType, ihdr);
    }

    private static void WriteTextChunk(Stream stream, string keyword, string value)
    {
        var keywordBytes = Encoding.Latin1.GetBytes(keyword);
        var valueBytes = Encoding.Latin1.GetBytes(value);
        var payload = new byte[keywordBytes.Length + 1 + valueBytes.Length];
        Buffer.BlockCopy(keywordBytes, 0, payload, 0, keywordBytes.Length);
        payload[keywordBytes.Length] = 0;
        Buffer.BlockCopy(valueBytes, 0, payload, keywordBytes.Length + 1, valueBytes.Length);
        WriteChunk(stream, TextType, payload);
    }

    private static void WriteIdat(Stream stream, D3D12Screenshot screenshot)
    {
        var stride = screenshot.Width * BytesPerPixel;
        var rawSize = checked((stride + 1) * screenshot.Height);
        var raw = new byte[rawSize];
        for (var y = 0; y < screenshot.Height; y++)
        {
            var dstRow = y * (stride + 1);
            raw[dstRow] = FilterNone;
            Buffer.BlockCopy(screenshot.Rgba8, y * stride, raw, dstRow + 1, stride);
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(raw, 0, raw.Length);
        }

        WriteChunk(stream, IdatType, compressed.GetBuffer().AsSpan(0, (int)compressed.Length));
    }

    private static void WriteChunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);
        stream.Write(length);
        stream.Write(type);
        if (data.Length > 0)
        {
            stream.Write(data);
        }

        var crc = ComputeCrc32(type, data);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        stream.Write(crcBytes);
    }

    private static uint ComputeCrc32(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var c = 0xFFFFFFFFu;
        for (var i = 0; i < type.Length; i++)
        {
            c = CrcTable[(c ^ type[i]) & 0xFF] ^ (c >> 8);
        }

        for (var i = 0; i < data.Length; i++)
        {
            c = CrcTable[(c ^ data[i]) & 0xFF] ^ (c >> 8);
        }

        return c ^ 0xFFFFFFFFu;
    }

    private static uint[] BuildCrcTable()
    {
        const uint polynomial = 0xEDB88320u;
        var table = new uint[256];
        for (uint i = 0; i < table.Length; i++)
        {
            var c = i;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1u) != 0u ? polynomial ^ (c >> 1) : c >> 1;
            }

            table[i] = c;
        }

        return table;
    }
}
