using System;
using FluentAssertions;
using Opus.Net.Transport;
using Opus.Net.Udp.Frame;
using Xunit;

namespace Opus.Net.Udp.Tests.Frame;

public sealed class UdpFrameCodecTests
{
    private static readonly ConnectionId SampleId = new(0x0102030405060708UL);
    private static readonly byte[] Key = UdpAuthentication.DeriveKey("frame-codec-tests-only");

    [Theory]
    [InlineData(UdpFrameKind.Hello, 0UL)]
    [InlineData(UdpFrameKind.WelcomeAck, 0UL)]
    [InlineData(UdpFrameKind.Payload, 12UL)]
    [InlineData(UdpFrameKind.Heartbeat, 13UL)]
    [InlineData(UdpFrameKind.Disconnect, 14UL)]
    public void Authenticated_frame_round_trips(UdpFrameKind kind, ulong sequence)
    {
        var id = kind == UdpFrameKind.Hello ? ConnectionId.None : SampleId;
        var payload = kind == UdpFrameKind.Payload ? new byte[] { 1, 2, 3, 4 } : Array.Empty<byte>();
        var buffer = new byte[
            UdpFrameHeader.SizeBytes + payload.Length + UdpFrameHeader.AuthenticationTagBytes];

        var written = UdpFrameCodec.EncodeAuthenticated(kind, id, sequence, payload, Key, buffer);

        written.Should().Be(buffer.Length);
        UdpFrameCodec.TryDecodeAuthenticated(buffer, Key, out var header, out var decoded).Should().BeTrue();
        header.Kind.Should().Be(kind);
        header.ConnectionId.Should().Be(id);
        header.Sequence.Should().Be(sequence);
        decoded.ToArray().Should().Equal(payload);
    }

    [Fact]
    public void Payload_or_header_tampering_is_rejected()
    {
        var buffer = EncodePayload();
        buffer[UdpFrameHeader.PayloadOffset] ^= 0x80;
        UdpFrameCodec.TryDecodeAuthenticated(buffer, Key, out _, out _).Should().BeFalse();

        buffer = EncodePayload();
        buffer[16] ^= 0x01;
        UdpFrameCodec.TryDecodeAuthenticated(buffer, Key, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Wrong_key_is_rejected()
    {
        var wrongKey = UdpAuthentication.DeriveKey("different-frame-codec-key");
        UdpFrameCodec.TryDecodeAuthenticated(EncodePayload(), wrongKey, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Envelope_can_be_inspected_without_authenticating()
    {
        var buffer = EncodePayload();

        UdpFrameCodec.TryDecodeEnvelope(buffer, out var header, out var payload, out var tag)
            .Should().BeTrue();
        header.Kind.Should().Be(UdpFrameKind.Payload);
        header.ConnectionId.Should().Be(SampleId);
        payload.ToArray().Should().Equal(0x10, 0x20, 0x30);
        tag.Length.Should().Be(UdpFrameHeader.AuthenticationTagBytes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(5)]
    public void Corrupt_envelope_is_rejected(int byteIndex)
    {
        var buffer = EncodePayload();
        buffer[byteIndex] = 0xFF;

        UdpFrameCodec.TryDecodeEnvelope(buffer, out _, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Truncation_and_trailing_bytes_are_rejected()
    {
        var buffer = EncodePayload();
        UdpFrameCodec.TryDecodeEnvelope(buffer.AsSpan(0, buffer.Length - 1), out _, out _, out _)
            .Should().BeFalse();

        var extended = new byte[buffer.Length + 1];
        buffer.CopyTo(extended, 0);
        UdpFrameCodec.TryDecodeEnvelope(extended, out _, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Oversized_payload_and_undersized_destination_are_rejected()
    {
        var oversized = new byte[UdpFrameHeader.MaxPayloadBytes + 1];
        var destination = new byte[
            UdpFrameHeader.SizeBytes + oversized.Length + UdpFrameHeader.AuthenticationTagBytes];

        Action oversizedAction = () => UdpFrameCodec.EncodeAuthenticated(
            UdpFrameKind.Payload,
            SampleId,
            sequence: 1,
            oversized,
            Key,
            destination);
        oversizedAction.Should().Throw<ArgumentException>().And.ParamName.Should().Be("payload");

        Action undersizedAction = () => UdpFrameCodec.EncodeAuthenticated(
            UdpFrameKind.Heartbeat,
            SampleId,
            sequence: 1,
            ReadOnlySpan<byte>.Empty,
            Key,
            new byte[UdpFrameHeader.SizeBytes]);
        undersizedAction.Should().Throw<ArgumentException>().And.ParamName.Should().Be("destination");
    }

    [Fact]
    public void Maximum_payload_round_trips()
    {
        var payload = new byte[UdpFrameHeader.MaxPayloadBytes];
        for (var index = 0; index < payload.Length; index++)
        {
            payload[index] = (byte)index;
        }

        var buffer = new byte[UdpFrameHeader.MaxDatagramBytes];
        UdpFrameCodec.EncodeAuthenticated(
            UdpFrameKind.Payload,
            SampleId,
            sequence: 1,
            payload,
            Key,
            buffer);

        UdpFrameCodec.TryDecodeAuthenticated(buffer, Key, out var header, out var decoded).Should().BeTrue();
        header.PayloadLength.Should().Be(UdpFrameHeader.MaxPayloadBytes);
        decoded.Length.Should().Be(payload.Length);
        decoded[^1].Should().Be(payload[^1]);
    }

    private static byte[] EncodePayload()
    {
        var payload = new byte[] { 0x10, 0x20, 0x30 };
        var buffer = new byte[
            UdpFrameHeader.SizeBytes + payload.Length + UdpFrameHeader.AuthenticationTagBytes];
        UdpFrameCodec.EncodeAuthenticated(
            UdpFrameKind.Payload,
            SampleId,
            sequence: 7,
            payload,
            Key,
            buffer);
        return buffer;
    }
}
