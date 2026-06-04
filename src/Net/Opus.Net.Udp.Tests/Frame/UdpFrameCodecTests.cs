using System;
using FluentAssertions;
using Opus.Net.Transport;
using Opus.Net.Udp.Frame;
using Xunit;

namespace Opus.Net.Udp.Tests.Frame;

public sealed class UdpFrameCodecTests
{
    private static readonly ConnectionId SampleId = new(0x0102030405060708UL);

    [Fact]
    public void Hello_round_trips_with_empty_payload_and_zero_id()
    {
        Span<byte> buffer = stackalloc byte[UdpFrameHeader.SizeBytes];
        var written = UdpFrameCodec.Encode(UdpFrameKind.Hello, ConnectionId.None, ReadOnlySpan<byte>.Empty, buffer);
        written.Should().Be(UdpFrameHeader.SizeBytes);

        UdpFrameCodec.TryDecode(buffer, out var header, out var payload).Should().BeTrue();
        header.Kind.Should().Be(UdpFrameKind.Hello);
        header.ConnectionId.Should().Be(ConnectionId.None);
        header.PayloadLength.Should().Be(0);
        payload.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void WelcomeAck_round_trips_with_assigned_id()
    {
        Span<byte> buffer = stackalloc byte[UdpFrameHeader.SizeBytes];
        UdpFrameCodec.Encode(UdpFrameKind.WelcomeAck, SampleId, ReadOnlySpan<byte>.Empty, buffer);

        UdpFrameCodec.TryDecode(buffer, out var header, out _).Should().BeTrue();
        header.Kind.Should().Be(UdpFrameKind.WelcomeAck);
        header.ConnectionId.Should().Be(SampleId);
    }

    [Fact]
    public void Payload_round_trips_byte_for_byte()
    {
        var bytes = new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50, 0x60 };
        Span<byte> buffer = stackalloc byte[UdpFrameHeader.SizeBytes + bytes.Length];

        var written = UdpFrameCodec.Encode(UdpFrameKind.Payload, SampleId, bytes, buffer);
        written.Should().Be(buffer.Length);

        UdpFrameCodec.TryDecode(buffer, out var header, out var payload).Should().BeTrue();
        header.Kind.Should().Be(UdpFrameKind.Payload);
        header.ConnectionId.Should().Be(SampleId);
        header.PayloadLength.Should().Be((ushort)bytes.Length);
        payload.ToArray().Should().Equal(bytes);
    }

    [Fact]
    public void Heartbeat_round_trips_with_no_payload()
    {
        Span<byte> buffer = stackalloc byte[UdpFrameHeader.SizeBytes];
        UdpFrameCodec.Encode(UdpFrameKind.Heartbeat, SampleId, ReadOnlySpan<byte>.Empty, buffer);

        UdpFrameCodec.TryDecode(buffer, out var header, out _).Should().BeTrue();
        header.Kind.Should().Be(UdpFrameKind.Heartbeat);
        header.PayloadLength.Should().Be(0);
    }

    [Fact]
    public void Disconnect_round_trips_with_no_payload()
    {
        Span<byte> buffer = stackalloc byte[UdpFrameHeader.SizeBytes];
        UdpFrameCodec.Encode(UdpFrameKind.Disconnect, SampleId, ReadOnlySpan<byte>.Empty, buffer);

        UdpFrameCodec.TryDecode(buffer, out var header, out _).Should().BeTrue();
        header.Kind.Should().Be(UdpFrameKind.Disconnect);
        header.ConnectionId.Should().Be(SampleId);
    }

    [Fact]
    public void Magic_prefix_is_GUDP()
    {
        Span<byte> buffer = stackalloc byte[UdpFrameHeader.SizeBytes];
        UdpFrameCodec.Encode(UdpFrameKind.Hello, ConnectionId.None, ReadOnlySpan<byte>.Empty, buffer);

        buffer[0].Should().Be((byte)'G');
        buffer[1].Should().Be((byte)'U');
        buffer[2].Should().Be((byte)'D');
        buffer[3].Should().Be((byte)'P');
    }

    [Fact]
    public void Wrong_magic_is_rejected()
    {
        var datagram = new byte[UdpFrameHeader.SizeBytes];
        datagram[0] = (byte)'X';
        datagram[1] = (byte)'U';
        datagram[2] = (byte)'D';
        datagram[3] = (byte)'P';
        datagram[4] = UdpFrameHeader.CurrentVersion;
        datagram[5] = (byte)UdpFrameKind.Hello;

        UdpFrameCodec.TryDecode(datagram, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Wrong_version_is_rejected()
    {
        Span<byte> buffer = stackalloc byte[UdpFrameHeader.SizeBytes];
        UdpFrameCodec.Encode(UdpFrameKind.Hello, ConnectionId.None, ReadOnlySpan<byte>.Empty, buffer);
        buffer[4] = 0xFF;

        UdpFrameCodec.TryDecode(buffer, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Unknown_kind_is_rejected()
    {
        Span<byte> buffer = stackalloc byte[UdpFrameHeader.SizeBytes];
        UdpFrameCodec.Encode(UdpFrameKind.Hello, ConnectionId.None, ReadOnlySpan<byte>.Empty, buffer);
        buffer[5] = 0xEE;

        UdpFrameCodec.TryDecode(buffer, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Invalid_kind_byte_zero_is_rejected()
    {
        Span<byte> buffer = stackalloc byte[UdpFrameHeader.SizeBytes];
        UdpFrameCodec.Encode(UdpFrameKind.Hello, ConnectionId.None, ReadOnlySpan<byte>.Empty, buffer);
        buffer[5] = (byte)UdpFrameKind.Invalid;

        UdpFrameCodec.TryDecode(buffer, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Truncated_buffer_below_header_size_is_rejected()
    {
        var datagram = new byte[UdpFrameHeader.SizeBytes - 1];
        UdpFrameCodec.TryDecode(datagram, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Truncated_payload_is_rejected()
    {
        var payload = new byte[10];
        Span<byte> full = stackalloc byte[UdpFrameHeader.SizeBytes + payload.Length];
        UdpFrameCodec.Encode(UdpFrameKind.Payload, SampleId, payload, full);

        var truncated = full[..(full.Length - 2)].ToArray();
        UdpFrameCodec.TryDecode(truncated, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Oversized_payload_throws_on_encode()
    {
        var oversized = new byte[UdpFrameHeader.MaxPayloadBytes + 1];
        var destination = new byte[UdpFrameHeader.SizeBytes + oversized.Length];

        var act = () => UdpFrameCodec.Encode(
            UdpFrameKind.Payload,
            SampleId,
            oversized,
            destination);

        act.Should().Throw<ArgumentException>().And.ParamName.Should().Be("payload");
    }

    [Fact]
    public void Undersized_destination_throws_on_encode()
    {
        Span<byte> tooSmall = stackalloc byte[UdpFrameHeader.SizeBytes - 1];
        var act = () =>
        {
            Span<byte> tiny = stackalloc byte[UdpFrameHeader.SizeBytes - 1];
            UdpFrameCodec.Encode(UdpFrameKind.Hello, ConnectionId.None, ReadOnlySpan<byte>.Empty, tiny);
        };

        act.Should().Throw<ArgumentException>().And.ParamName.Should().Be("destination");
    }

    [Fact]
    public void Connection_id_is_written_little_endian()
    {
        Span<byte> buffer = stackalloc byte[UdpFrameHeader.SizeBytes];
        UdpFrameCodec.Encode(UdpFrameKind.WelcomeAck, SampleId, ReadOnlySpan<byte>.Empty, buffer);

        buffer[8].Should().Be(0x08);
        buffer[9].Should().Be(0x07);
        buffer[10].Should().Be(0x06);
        buffer[11].Should().Be(0x05);
        buffer[12].Should().Be(0x04);
        buffer[13].Should().Be(0x03);
        buffer[14].Should().Be(0x02);
        buffer[15].Should().Be(0x01);
    }

    [Fact]
    public void Payload_length_is_written_little_endian()
    {
        var payload = new byte[0x0102];
        var buffer = new byte[UdpFrameHeader.SizeBytes + payload.Length];
        UdpFrameCodec.Encode(UdpFrameKind.Payload, SampleId, payload, buffer);

        buffer[6].Should().Be(0x02);
        buffer[7].Should().Be(0x01);
    }

    [Fact]
    public void Max_size_payload_round_trips()
    {
        var payload = new byte[UdpFrameHeader.MaxPayloadBytes];
        for (var i = 0; i < payload.Length; i++)
        {
            payload[i] = (byte)(i & 0xFF);
        }

        var buffer = new byte[UdpFrameHeader.MaxDatagramBytes];
        UdpFrameCodec.Encode(UdpFrameKind.Payload, SampleId, payload, buffer);

        UdpFrameCodec.TryDecode(buffer, out var header, out var decoded).Should().BeTrue();
        header.PayloadLength.Should().Be(UdpFrameHeader.MaxPayloadBytes);
        decoded.Length.Should().Be(payload.Length);
        decoded[0].Should().Be(payload[0]);
        decoded[payload.Length - 1].Should().Be(payload[^1]);
    }
}
