using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using Opus.Net.Transport;

namespace Opus.Net.Udp.Frame;

/// <summary>
/// Pure encoder and decoder for authenticated <see cref="UdpFrameHeader"/> frames.
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
    public static int EncodeAuthenticated(
        UdpFrameKind kind,
        ConnectionId connectionId,
        ulong sequence,
        ReadOnlySpan<byte> payload,
        ReadOnlySpan<byte> authenticationKey,
        Span<byte> destination)
    {
        ValidateAuthenticationKey(authenticationKey);
        if (payload.Length > UdpFrameHeader.MaxPayloadBytes)
        {
            throw new ArgumentException(
                $"UDP payload exceeds {UdpFrameHeader.MaxPayloadBytes} bytes (got {payload.Length}).",
                nameof(payload));
        }

        var unsignedBytes = UdpFrameHeader.SizeBytes + payload.Length;
        var totalBytes = unsignedBytes + UdpFrameHeader.AuthenticationTagBytes;
        if (destination.Length < totalBytes)
        {
            throw new ArgumentException(
                $"UDP destination too small: need {totalBytes} bytes, have {destination.Length}.",
                nameof(destination));
        }

        WriteHeader(destination, kind, connectionId, sequence, (ushort)payload.Length);
        if (!payload.IsEmpty)
        {
            payload.CopyTo(destination[UdpFrameHeader.PayloadOffset..]);
        }

        HMACSHA256.HashData(
            authenticationKey,
            destination[..unsignedBytes],
            destination.Slice(unsignedBytes, UdpFrameHeader.AuthenticationTagBytes));
        return totalBytes;
    }

    /// <summary>Attempts to parse a frame out of <paramref name="datagram"/>. On success
    /// fills <paramref name="header"/> + <paramref name="payload"/> (the payload span aliases
    /// the input — do not retain it past the buffer's lifetime). Returns false when the
    /// magic, version, kind, or declared length is unrecognised / inconsistent.</summary>
    public static bool TryDecodeAuthenticated(
        ReadOnlySpan<byte> datagram,
        ReadOnlySpan<byte> authenticationKey,
        out UdpFrameHeader header,
        out ReadOnlySpan<byte> payload)
    {
        ValidateAuthenticationKey(authenticationKey);
        if (!TryDecodeEnvelope(datagram, out header, out payload, out var suppliedTag))
        {
            return false;
        }

        Span<byte> expectedTag = stackalloc byte[UdpFrameHeader.AuthenticationTagBytes];
        var authenticatedBytes = UdpFrameHeader.SizeBytes + header.PayloadLength;
        HMACSHA256.HashData(authenticationKey, datagram[..authenticatedBytes], expectedTag);
        return CryptographicOperations.FixedTimeEquals(expectedTag, suppliedTag);
    }

    /// <summary>Parses the public frame envelope without trusting it. This is used only by
    /// handshake code that must derive the session key from the welcome payload before it can
    /// authenticate that same datagram.</summary>
    public static bool TryDecodeEnvelope(
        ReadOnlySpan<byte> datagram,
        out UdpFrameHeader header,
        out ReadOnlySpan<byte> payload,
        out ReadOnlySpan<byte> authenticationTag)
    {
        header = default;
        payload = default;
        authenticationTag = default;

        if (datagram.Length < UdpFrameHeader.SizeBytes + UdpFrameHeader.AuthenticationTagBytes)
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
        var sequence = BinaryPrimitives.ReadUInt64LittleEndian(datagram.Slice(16, 8));

        var expectedTotal = UdpFrameHeader.SizeBytes + payloadLength + UdpFrameHeader.AuthenticationTagBytes;
        if (datagram.Length != expectedTotal)
        {
            return false;
        }

        header = new UdpFrameHeader(
            (UdpFrameKind)kindByte,
            new ConnectionId(connectionIdValue),
            payloadLength,
            sequence);
        payload = payloadLength == 0
            ? ReadOnlySpan<byte>.Empty
            : datagram.Slice(UdpFrameHeader.PayloadOffset, payloadLength);
        authenticationTag = datagram[^UdpFrameHeader.AuthenticationTagBytes..];
        return true;
    }

    /// <summary>Writes the fixed 16-byte header at the start of <paramref name="destination"/>.
    /// Caller must guarantee at least <see cref="UdpFrameHeader.SizeBytes"/> bytes are
    /// available.</summary>
    private static void WriteHeader(
        Span<byte> destination,
        UdpFrameKind kind,
        ConnectionId connectionId,
        ulong sequence,
        ushort payloadLength)
    {
        UdpFrameHeader.Magic.CopyTo(destination);
        destination[4] = UdpFrameHeader.CurrentVersion;
        destination[5] = (byte)kind;
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(6, 2), payloadLength);
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(8, 8), connectionId.Value);
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(16, 8), sequence);
    }

    private static bool IsKnownKind(byte value) =>
        value is >= (byte)UdpFrameKind.Hello and <= (byte)UdpFrameKind.WelcomeConfirm;

    private static void ValidateAuthenticationKey(ReadOnlySpan<byte> authenticationKey)
    {
        if (authenticationKey.Length < UdpAuthentication.MinimumKeyBytes)
        {
            throw new ArgumentException(
                $"UDP authentication keys must contain at least {UdpAuthentication.MinimumKeyBytes} bytes.",
                nameof(authenticationKey));
        }
    }
}
