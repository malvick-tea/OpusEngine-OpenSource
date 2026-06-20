using System;
using System.Security.Cryptography;
using FluentAssertions;
using Opus.Net.Transport;
using Opus.Net.Udp.Frame;
using Xunit;

namespace Opus.Net.Udp.Tests.Frame;

/// <summary>Adversarial regression tests for the security fixes landed in
/// Audit V2. Each test pins one of the new protocol properties
/// (direction-specific keys, sliding-window replay, 3-way handshake
/// primitives) so a future refactor that accidentally re-introduces the
/// original vulnerability fails loudly.</summary>
public sealed class UdpSecurityRegressionTests
{
    private static readonly byte[] Psk = UdpAuthentication.DeriveKey("audit-v2-regression-psk");
    private static readonly ConnectionId SampleId = new(0x0908070605040302UL);

    [Fact]
    public void Direction_keys_are_independent_for_c2s_and_s2c()
    {
        var sessionKey = UdpAuthentication.DeriveSessionKey(
            Psk,
            UdpAuthentication.CreateNonce(),
            UdpAuthentication.CreateNonce(),
            SampleId);

        var c2s = UdpAuthentication.DeriveDirectionKey(
            sessionKey, SampleId, UdpAuthentication.Direction.ClientToServer);
        var s2c = UdpAuthentication.DeriveDirectionKey(
            sessionKey, SampleId, UdpAuthentication.Direction.ServerToClient);

        c2s.Should().NotEqual(s2c, "direction-specific keys must be independent so a frame MAC'd in one direction never verifies when reflected back to the sender");
        c2s.Length.Should().Be(UdpAuthentication.DirectionKeyBytes);
        s2c.Length.Should().Be(UdpAuthentication.DirectionKeyBytes);
    }

    [Fact]
    public void Direction_keys_change_when_connection_id_changes()
    {
        var nonce = UdpAuthentication.CreateNonce();
        var sessionKeyA = UdpAuthentication.DeriveSessionKey(Psk, nonce, nonce, SampleId);
        var sessionKeyB = UdpAuthentication.DeriveSessionKey(Psk, nonce, nonce, new ConnectionId(SampleId.Value + 1UL));

        var keyA = UdpAuthentication.DeriveDirectionKey(
            sessionKeyA, SampleId, UdpAuthentication.Direction.ClientToServer);
        var keyB = UdpAuthentication.DeriveDirectionKey(
            sessionKeyB, new ConnectionId(SampleId.Value + 1UL), UdpAuthentication.Direction.ClientToServer);

        keyA.Should().NotEqual(keyB, "direction keys must fold in the connection id so two sessions on the same PSK cannot reuse a key");
    }

    [Fact]
    public void Reflected_c2s_frame_fails_mac_verify_with_s2c_key()
    {
        // The reflection attack (audit finding N-2): an on-path attacker
        // captures a client -> server frame and reflects it back to the
        // client. Pre-fix, the same session key was used both directions
        // so the reflected frame's MAC verified. Post-fix, the client
        // verifies inbound frames with the s2c key; a frame MAC'd with
        // the c2s key must fail.
        var sessionKey = UdpAuthentication.DeriveSessionKey(
            Psk,
            UdpAuthentication.CreateNonce(),
            UdpAuthentication.CreateNonce(),
            SampleId);

        var c2s = UdpAuthentication.DeriveDirectionKey(
            sessionKey, SampleId, UdpAuthentication.Direction.ClientToServer);
        var s2c = UdpAuthentication.DeriveDirectionKey(
            sessionKey, SampleId, UdpAuthentication.Direction.ServerToClient);

        var payload = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var buffer = new byte[
            UdpFrameHeader.SizeBytes + payload.Length + UdpFrameHeader.AuthenticationTagBytes];
        UdpFrameCodec.EncodeAuthenticated(
            UdpFrameKind.Payload,
            SampleId,
            sequence: 1UL,
            payload,
            c2s,
            buffer);

        // The client verifies with s2c — the reflected frame must fail.
        UdpFrameCodec.TryDecodeAuthenticated(buffer, s2c, out _, out _)
            .Should().BeFalse("a c2s frame reflected back to the client must fail MAC verification under the s2c key");
    }

    [Fact]
    public void WelcomeConfirm_frame_round_trips_through_codec()
    {
        // The new WelcomeConfirm frame kind (third leg of the 3-way
        // handshake) must round-trip through the codec so the server can
        // parse what the client sends.
        var challenge = UdpAuthentication.CreateNonce();
        var sessionKey = UdpAuthentication.DeriveSessionKey(
            Psk,
            UdpAuthentication.CreateNonce(),
            challenge,
            SampleId);
        var c2s = UdpAuthentication.DeriveDirectionKey(
            sessionKey, SampleId, UdpAuthentication.Direction.ClientToServer);

        var buffer = new byte[
            UdpFrameHeader.SizeBytes + challenge.Length + UdpFrameHeader.AuthenticationTagBytes];
        UdpFrameCodec.EncodeAuthenticated(
            UdpFrameKind.WelcomeConfirm,
            SampleId,
            sequence: 0UL,
            challenge,
            c2s,
            buffer);

        UdpFrameCodec.TryDecodeAuthenticated(buffer, c2s, out var header, out var payload)
            .Should().BeTrue();
        header.Kind.Should().Be(UdpFrameKind.WelcomeConfirm);
        header.ConnectionId.Should().Be(SampleId);
        header.Sequence.Should().Be(0UL);
        payload.ToArray().Should().Equal(challenge);
    }

    [Fact]
    public void WelcomeConfirm_frame_rejected_under_s2c_key()
    {
        // A WelcomeConfirm is MAC'd with the c2s direction key (client ->
        // server). The server verifies with the c2s key, never the s2c
        // key. An attacker who somehow obtains the s2c key but not the
        // c2s key must not be able to forge a WelcomeConfirm.
        var challenge = UdpAuthentication.CreateNonce();
        var sessionKey = UdpAuthentication.DeriveSessionKey(
            Psk,
            UdpAuthentication.CreateNonce(),
            challenge,
            SampleId);
        var c2s = UdpAuthentication.DeriveDirectionKey(
            sessionKey, SampleId, UdpAuthentication.Direction.ClientToServer);
        var s2c = UdpAuthentication.DeriveDirectionKey(
            sessionKey, SampleId, UdpAuthentication.Direction.ServerToClient);

        var buffer = new byte[
            UdpFrameHeader.SizeBytes + challenge.Length + UdpFrameHeader.AuthenticationTagBytes];
        UdpFrameCodec.EncodeAuthenticated(
            UdpFrameKind.WelcomeConfirm,
            SampleId,
            sequence: 0UL,
            challenge,
            c2s,
            buffer);

        UdpFrameCodec.TryDecodeAuthenticated(buffer, s2c, out _, out _)
            .Should().BeFalse("a WelcomeConfirm MAC'd under the c2s key must not verify under the s2c key");
    }
}
