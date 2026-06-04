using System;
using System.Buffers.Binary;
using Opus.Net.Transport;

namespace Opus.Net.Udp.Frame;

/// <summary>
/// Pure, allocation-free encoder / decoder for <see cref="UdpFrameHeader"/> + payload.
/// No socket dependency; every method takes a <see cref="Span{T}"/> over a caller-owned
/// buffer so the codec is unit-testable headless and the same primitive serves both the
/// client and server transports.
/// </summary>
/// <remarks>
/// All multi-byte fields are written little-endian to match how every modern desktop
/// platform sits in memory; the codec never converts on platforms it isn't compiled for.
/// </remarks>
public static class UdpFrameCodec
{
    /// <summary>Writes a full frame (header + payload) into <paramref name="destination"/>.
    /// Returns the number of bytes consumed — always <c>SizeBytes + payload.Length</c>.
    /// Throws <see cref="ArgumentException"/> when the destination is too small or the
    /// payload exceeds <see cref="UdpFrameHeader.MaxPayloadBytes"/>.</summary>
    public static int Encode(
        UdpFrameKind kind,
        ConnectionId connectionId,
        ReadOnlySpan<byte> payload,
        Span<byte> destination)
    {
        if (payload.Length > UdpFrameHeader.MaxPayloadBytes)
        {
            throw new ArgumentException(
                $"UDP payload exceeds {UdpFrameHeader.MaxPayloadBytes} bytes (got {payload.Length}).",
                nameof(payload));
        }

        var totalBytes = UdpFrameHeader.SizeBytes + payload.Length;
        if (destination.Length < totalBytes)
        {
            throw new ArgumentException(
                $"UDP destination too small: need {totalBytes} bytes, have {destination.Length}.",
                nameof(destination));
        }

        WriteHeader(destination, kind, connectionId, (ushort)payload.Length);
        if (!payload.IsEmpty)
        {
            payload.CopyTo(destination[UdpFrameHeader.PayloadOffset..]);
        }

        return totalBytes;
    }

    /// <summary>Attempts to parse a frame out of <paramref name="datagram"/>. On success
    /// fills <paramref name="header"/> + <paramref name="payload"/> (the payload span aliases
    /// the input — do not retain it past the buffer's lifetime). Returns false when the
    /// magic, version, kind, or declared length is unrecognised / inconsistent.</summary>
    public static bool TryDecode(
        ReadOnlySpan<byte> datagram,
        out UdpFrameHeader header,
        out ReadOnlySpan<byte> payload)
    {
        header = default;
        payload = default;

        if (datagram.Length < UdpFrameHeader.SizeBytes)
        {
            return false;
        }

        if (!datagram[..UdpFrameHeader.Magic.Length].SequenceEqual(UdpFrameHeader.Magic))
        {
            return false;
        }

        if (datagram[4] != UdpFrameHeader.CurrentVersion)
        {
            return false;
        }

        var kindByte = datagram[5];
        if (!IsKnownKind(kindByte))
        {
            return false;
        }

        var payloadLength = BinaryPrimitives.ReadUInt16LittleEndian(datagram.Slice(6, 2));
        var connectionIdValue = BinaryPrimitives.ReadUInt64LittleEndian(datagram.Slice(8, 8));

        var expectedTotal = UdpFrameHeader.SizeBytes + payloadLength;
        if (datagram.Length < expectedTotal)
        {
            return false;
        }

        header = new UdpFrameHeader((UdpFrameKind)kindByte, new ConnectionId(connectionIdValue), payloadLength);
        payload = payloadLength == 0
            ? ReadOnlySpan<byte>.Empty
            : datagram.Slice(UdpFrameHeader.PayloadOffset, payloadLength);
        return true;
    }

    /// <summary>Writes the fixed 16-byte header at the start of <paramref name="destination"/>.
    /// Caller must guarantee at least <see cref="UdpFrameHeader.SizeBytes"/> bytes are
    /// available.</summary>
    private static void WriteHeader(
        Span<byte> destination,
        UdpFrameKind kind,
        ConnectionId connectionId,
        ushort payloadLength)
    {
        UdpFrameHeader.Magic.CopyTo(destination);
        destination[4] = UdpFrameHeader.CurrentVersion;
        destination[5] = (byte)kind;
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(6, 2), payloadLength);
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(8, 8), connectionId.Value);
    }

    private static bool IsKnownKind(byte value) =>
        value is >= (byte)UdpFrameKind.Hello and <= (byte)UdpFrameKind.Disconnect;
}
