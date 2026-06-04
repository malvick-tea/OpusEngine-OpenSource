using System;
using FluentAssertions;
using Opus.Engine.Net.Soak;
using Xunit;

namespace Opus.Engine.Net.Tests.Soak;

/// <summary>Pure encode/decode coverage for the 16-byte <see cref="NetSoakPacketHeader"/>.</summary>
public sealed class NetSoakPacketHeaderTests
{
    [Fact]
    public void Encode_then_decode_round_trips_metadata_and_payload()
    {
        var header = new NetSoakPacketHeader(PeerIndex: 3, SequenceNumber: 17, PayloadLength: 5);
        var payload = new byte[] { 9, 8, 7, 6, 5 };
        var buffer = new byte[NetSoakPacketHeader.SizeBytes + payload.Length];

        NetSoakPacketHeader.Encode(header, payload, buffer).Should().Be(buffer.Length);

        NetSoakPacketHeader.TryDecode(buffer, out var decoded, out var decodedPayload).Should().BeTrue();
        decoded.Should().Be(header);
        decodedPayload.ToArray().Should().Equal(payload);
    }

    [Fact]
    public void TryDecode_rejects_buffers_smaller_than_header()
    {
        var bad = new byte[NetSoakPacketHeader.SizeBytes - 1];
        NetSoakPacketHeader.TryDecode(bad, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void TryDecode_rejects_bad_magic()
    {
        var buffer = new byte[NetSoakPacketHeader.SizeBytes + 4];
        var header = new NetSoakPacketHeader(0, 0, 4);
        NetSoakPacketHeader.Encode(header, new byte[] { 1, 2, 3, 4 }, buffer);
        buffer[0] = 0x42;

        NetSoakPacketHeader.TryDecode(buffer, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void TryDecode_rejects_truncated_payload()
    {
        var buffer = new byte[NetSoakPacketHeader.SizeBytes + 4];
        var header = new NetSoakPacketHeader(0, 0, 4);
        NetSoakPacketHeader.Encode(header, new byte[] { 1, 2, 3, 4 }, buffer);

        var truncated = buffer.AsSpan(0, NetSoakPacketHeader.SizeBytes + 2);
        NetSoakPacketHeader.TryDecode(truncated, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Encode_rejects_payload_length_mismatch()
    {
        var header = new NetSoakPacketHeader(0, 0, 3);
        var buffer = new byte[NetSoakPacketHeader.SizeBytes + 2];

        Action act = () => NetSoakPacketHeader.Encode(header, new byte[] { 1, 2 }, buffer);
        act.Should().Throw<ArgumentException>();
    }
}
