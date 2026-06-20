using System;
using System.Diagnostics.CodeAnalysis;

namespace Opus.Engine.Ui.Text;

/// <summary>
/// Extracts a single member font from a TrueType Collection (<c>.ttc</c>) as a standalone
/// sfnt byte buffer. stb_truetype-family loaders are handed a fixed file offset of zero by
/// some callers, so they cannot open a font nested inside a collection — yet every Windows
/// Japanese font (Yu Gothic, MS Gothic) ships only as a <c>.ttc</c>. Rebasing one member's
/// table directory into its own buffer makes it loadable as if it were a plain <c>.ttf</c>.
///
/// Backend-agnostic: lives in Engine.Ui so both the Raylib and Direct3D12 text backends
/// can lift a member font out of a system <c>.ttc</c>.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "\"TrueType Collection\" is the .ttc file-format's own name — the type models that format, it is not a C# collection.")]
public static class TrueTypeCollection
{
    private const uint CollectionTag = 0x74746366;     // 'ttcf'
    private const int CollectionHeaderSize = 12;       // tag + version + font count
    private const int SfntHeaderSize = 12;             // sfnt version + table count + 3 search hints
    private const int TableRecordSize = 16;            // tag + checksum + offset + length

    /// <summary>Builds a standalone sfnt for member font <paramref name="fontIndex"/>;
    /// returns <c>null</c> when the bytes are not a TTC or the font / its tables fall
    /// outside the buffer.</summary>
    public static byte[]? ExtractFont(byte[] collection, int fontIndex)
    {
        if (collection.Length < CollectionHeaderSize + 4 ||
            ReadUInt32(collection, 0) != CollectionTag)
        {
            return null;
        }

        var fontCount = ReadUInt32(collection, 8);
        if (fontIndex < 0 || (uint)fontIndex >= fontCount)
        {
            return null;
        }

        var directoryPointer = (long)CollectionHeaderSize + ((long)fontIndex * sizeof(uint));
        return directoryPointer + sizeof(uint) > collection.Length
            ? null
            : BuildStandaloneFont(collection, ReadUInt32(collection, (int)directoryPointer));
    }

    private static byte[]? BuildStandaloneFont(byte[] ttc, uint directoryOffsetValue)
    {
        if (directoryOffsetValue > int.MaxValue)
        {
            return null;
        }

        var directoryOffset = (int)directoryOffsetValue;
        if ((long)directoryOffset + SfntHeaderSize > ttc.Length)
        {
            return null;
        }

        int tableCount = ReadUInt16(ttc, directoryOffset + 4);
        var recordsStart = (long)directoryOffset + SfntHeaderSize;
        var recordsBytes = (long)tableCount * TableRecordSize;
        var bodyStart = (long)SfntHeaderSize + recordsBytes;
        if (recordsStart + recordsBytes > ttc.Length)
        {
            return null;
        }

        // Pass one: validate every table and place its data in the standalone buffer,
        // each table 4-byte aligned as the sfnt spec requires.
        var placedOffsets = new int[tableCount];
        var totalSize = bodyStart;
        for (var i = 0; i < tableCount; i++)
        {
            var record = checked((int)(recordsStart + ((long)i * TableRecordSize)));
            var dataOffset = ReadUInt32(ttc, record + 8);
            var dataLength = ReadUInt32(ttc, record + 12);
            if ((long)dataOffset + dataLength > ttc.Length)
            {
                return null;
            }

            if (totalSize > int.MaxValue)
            {
                return null;
            }

            placedOffsets[i] = (int)totalSize;
            totalSize += Align4(dataLength);
        }

        if (totalSize > int.MaxValue)
        {
            return null;
        }

        var output = new byte[(int)totalSize];

        // The sfnt header (version + table count + search hints) carries over verbatim.
        Array.Copy(ttc, directoryOffset, output, 0, SfntHeaderSize);

        // Pass two: copy each record with a rebased offset, then its table data. Per-table
        // checksums stay valid (they cover content, not position); only head's global
        // checkSumAdjustment goes stale, which stb_truetype never verifies.
        for (var i = 0; i < tableCount; i++)
        {
            var source = checked((int)(recordsStart + ((long)i * TableRecordSize)));
            var dataOffset = checked((int)ReadUInt32(ttc, source + 8));
            var dataLength = checked((int)ReadUInt32(ttc, source + 12));
            var destination = SfntHeaderSize + (i * TableRecordSize);

            Array.Copy(ttc, source, output, destination, 8);   // tag + checksum, unchanged
            WriteUInt32(output, destination + 8, (uint)placedOffsets[i]);
            WriteUInt32(output, destination + 12, (uint)dataLength);
            Array.Copy(ttc, dataOffset, output, placedOffsets[i], dataLength);
        }

        return output;
    }

    private static long Align4(uint value) => ((long)value + 3) & ~3L;

    private static uint ReadUInt32(byte[] data, int offset) =>
        ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) |
        ((uint)data[offset + 2] << 8) | data[offset + 3];

    private static ushort ReadUInt16(byte[] data, int offset) =>
        (ushort)((data[offset] << 8) | data[offset + 1]);

    private static void WriteUInt32(byte[] data, int offset, uint value)
    {
        data[offset] = (byte)(value >> 24);
        data[offset + 1] = (byte)(value >> 16);
        data[offset + 2] = (byte)(value >> 8);
        data[offset + 3] = (byte)value;
    }
}
