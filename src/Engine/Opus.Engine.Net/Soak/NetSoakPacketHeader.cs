using System;
using System.Buffers.Binary;

namespace Opus.Engine.Net.Soak;

/// <summary>
/// Fixed 16-byte header the soak harness prepends to every workload datagram. Layout
/// (little-endian):
/// <code>
/// [0..3]   magic 'O' 'S' 'O' 'K'   (4 bytes)
/// [4..7]   peer index (uint32)     (4 bytes)
/// [8..11]  sequence number (uint32) (4 bytes)
/// [12..15] payload length (uint32) (4 bytes)
/// </code>
/// The header lets the receiver verify origin, detect dropped sequences, and reject
/// stray non-soak traffic without allocating any state. Separate from
/// <c>Opus.Net.Udp.Frame.UdpFrameHeader</c> because the soak workload runs above the
/// transport boundary — the soak harness has to operate identically over loopback and
/// over UDP, so its framing cannot piggyback on UDP-specific fields.
/// </summary>
public readonly record struct NetSoakPacketHeader(uint PeerIndex, uint SequenceNumber, uint PayloadLength)
{
    /// <summary>Fixed header byte count. Payload bytes follow inline.</summary>
    public const int SizeBytes = 16;

    /// <summary>ASCII magic prefix — 'O' 'S' 'O' 'K'. Datagrams without this prefix are
    /// not soak traffic and must be ignored.</summary>
    public static ReadOnlySpan<byte> Magic => "OSOK"u8;

    /// <summary>Encodes a complete soak datagram (header + payload) into
    /// <paramref name="destination"/>. Returns total bytes written. Throws when the
    /// destination is too small to hold the header plus the declared payload.</summary>
    public static int Encode(NetSoakPacketHeader header, ReadOnlySpan<byte> payload, Span<byte> destination)
    {
        if (payload.Length != header.PayloadLength)
        {
            throw new ArgumentException(
                $"Payload length ({payload.Length}) does not match header ({header.PayloadLength}).",
                nameof(payload));
        }

        var totalBytes = SizeBytes + payload.Length;
        if (destination.Length < totalBytes)
        {
            throw new ArgumentException(
                $"NetSoakPacketHeader destination too small: need {totalBytes} bytes, have {destination.Length}.",
                nameof(destination));
        }

        Magic.CopyTo(destination);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(4, 4), header.PeerIndex);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(8, 4), header.SequenceNumber);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(12, 4), header.PayloadLength);
        if (!payload.IsEmpty)
        {
            payload.CopyTo(destination[SizeBytes..]);
        }

        return totalBytes;
    }

    /// <summary>Attempts to decode a header from <paramref name="datagram"/>. On success,
    /// <paramref name="payload"/> aliases the input span — do not retain past the
    /// buffer's lifetime. Returns false when the magic mismatches, the declared length
    /// runs past the buffer, or the payload length is too large to be plausible
    /// workload data.</summary>
    public static bool TryDecode(
        ReadOnlySpan<byte> datagram,
        out NetSoakPacketHeader header,
        out ReadOnlySpan<byte> payload)
    {
        header = default;
        payload = default;

        if (datagram.Length < SizeBytes)
        {
            return false;
        }

        if (!datagram[..Magic.Length].SequenceEqual(Magic))
        {
            return false;
        }

        var peerIndex = BinaryPrimitives.ReadUInt32LittleEndian(datagram.Slice(4, 4));
        var sequence = BinaryPrimitives.ReadUInt32LittleEndian(datagram.Slice(8, 4));
        var payloadLength = BinaryPrimitives.ReadUInt32LittleEndian(datagram.Slice(12, 4));
        if (payloadLength > int.MaxValue - SizeBytes)
        {
            return false;
        }

        var expectedTotal = SizeBytes + (int)payloadLength;
        if (datagram.Length < expectedTotal)
        {
            return false;
        }

        header = new NetSoakPacketHeader(peerIndex, sequence, payloadLength);
        payload = payloadLength == 0
            ? ReadOnlySpan<byte>.Empty
            : datagram.Slice(SizeBytes, (int)payloadLength);
        return true;
    }
}
