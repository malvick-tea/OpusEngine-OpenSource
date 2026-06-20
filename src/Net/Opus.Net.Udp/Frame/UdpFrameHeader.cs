using System;
using System.Runtime.InteropServices;
using Opus.Net.Transport;

namespace Opus.Net.Udp.Frame;

/// <summary>
/// Fixed UDP frame header. Layout (little-endian, byte offsets):
/// <code>
/// [0..3]   magic 'G' 'U' 'D' 'P'   (4 bytes)
/// [4]      protocol version         (1 byte)
/// [5]      kind (UdpFrameKind)      (1 byte)
/// [6..7]   payload length (UInt16)  (2 bytes)
/// [8..15]  connection id (UInt64)   (8 bytes)
/// [16..23] sequence number (UInt64)  (8 bytes)
/// </code>
/// The payload follows the header and a 32-byte HMAC-SHA256 tag terminates the datagram.
/// </summary>
/// <remarks>
/// The magic + version pair lets the receiver reject stray non-Opus datagrams in a
/// shared port environment (NAT hairpinning, port scanners, spurious VPN traffic) before
/// allocating any state. The 16-bit payload length caps every datagram at
/// <see cref="MaxPayloadBytes"/> = 65 535; well above the IPv4 MTU floor (1500) and inside
/// the UDP datagram cap (65 507), so this is informational not constraining.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
public readonly record struct UdpFrameHeader(
    UdpFrameKind Kind,
    ConnectionId ConnectionId,
    ushort PayloadLength,
    ulong Sequence)
{
    /// <summary>Header byte count. The payload follows immediately after.</summary>
    public const int SizeBytes = 24;

    /// <summary>Authentication tag byte count appended after the payload.</summary>
    public const int AuthenticationTagBytes = 32;

    /// <summary>Protocol version. Bump when the wire shape changes; old peers see a
    /// version mismatch and drop the frame instead of misparsing.</summary>
    public const byte CurrentVersion = 2;

    /// <summary>Largest payload one datagram can carry — limited by the 16-bit length
    /// field. Bytes; not packets per second.</summary>
    public const ushort MaxPayloadBytes = 65451;

    /// <summary>Offset (from the start of the buffer) where the payload begins. Mirrors
    /// <see cref="SizeBytes"/> — separate name for readability at codec call sites.</summary>
    public const int PayloadOffset = SizeBytes;

    /// <summary>Maximum total datagram size, header + payload — useful for sizing send
    /// scratch buffers without two adds at the call site.</summary>
    public const int MaxDatagramBytes = SizeBytes + MaxPayloadBytes + AuthenticationTagBytes;

    /// <summary>Fixed magic prefix — ASCII 'G','U','D','P'. Datagrams without this prefix
    /// are not Opus UDP traffic and must be silently dropped.</summary>
    public static ReadOnlySpan<byte> Magic => "GUDP"u8;
}
