using System;
using System.Security.Cryptography;
using System.Threading;
using Opus.Net.Transport;
using Opus.Net.Udp.Frame;

namespace Opus.Net.Udp.Transport;

public sealed partial class UdpClientTransport
{
    private readonly object _securitySync = new();
    private readonly object _replayWindowLock = new();
    private readonly byte[] _clientNonce = UdpAuthentication.CreateNonce();
    private byte[]? _sessionKey;
    private byte[]? _inboundKey;
    private byte[]? _outboundKey;
    private long _nextOutboundSequence;

    // Anti-replay window for server -> client frames. Mirrors the server-side
    // design in UdpServerPeerSlot: 64-sequence sliding bitfield, fast-forward
    // on a fresh high sequence, reject anything below the trailing edge, and
    // reject duplicates inside the window. Allows legitimate UDP reordering
    // (0.05-1% on consumer links) without dropping frames.
    private const int ReplayWindowSlots = 64;
    private ulong _highestInboundSequence;
    private ulong _inboundWindowBitfieldLow;
    private ulong _inboundWindowBitfieldHigh;

    private bool TryDecodeInbound(
        ReadOnlySpan<byte> datagram,
        out UdpFrameHeader header,
        out ReadOnlySpan<byte> payload)
    {
        header = default;
        payload = default;

        if (!UdpFrameCodec.TryDecodeEnvelope(datagram, out var envelope, out var untrustedPayload, out _))
        {
            return false;
        }

        if (Volatile.Read(ref _state) == StateConnecting)
        {
            return TryAcceptWelcome(datagram, envelope, untrustedPayload, out header, out payload);
        }

        byte[]? inboundKey;
        lock (_securitySync)
        {
            inboundKey = _inboundKey;
            if (inboundKey is null
                || envelope.ConnectionId.Value != Volatile.Read(ref _assignedId)
                || !UdpFrameCodec.TryDecodeAuthenticated(datagram, inboundKey, out header, out payload)
                || header.Sequence == 0)
            {
                return false;
            }
        }

        lock (_replayWindowLock)
        {
            if (!TryAcceptInboundSequence(header.Sequence))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryAcceptWelcome(
        ReadOnlySpan<byte> datagram,
        UdpFrameHeader envelope,
        ReadOnlySpan<byte> untrustedPayload,
        out UdpFrameHeader header,
        out ReadOnlySpan<byte> payload)
    {
        header = default;
        payload = default;
        if (envelope.Kind != UdpFrameKind.WelcomeAck
            || !envelope.ConnectionId.IsValid
            || envelope.Sequence != 0
            || untrustedPayload.Length != UdpAuthentication.NonceBytes)
        {
            return false;
        }

        var candidateSessionKey = UdpAuthentication.DeriveSessionKey(
            _authenticationKey,
            _clientNonce,
            untrustedPayload,
            envelope.ConnectionId);
        var candidateInbound = UdpAuthentication.DeriveDirectionKey(
            candidateSessionKey,
            envelope.ConnectionId,
            UdpAuthentication.Direction.ServerToClient);
        var candidateOutbound = UdpAuthentication.DeriveDirectionKey(
            candidateSessionKey,
            envelope.ConnectionId,
            UdpAuthentication.Direction.ClientToServer);

        // The WelcomeAck frame is MAC'd with the server -> client direction
        // key. If the MAC fails the candidate keys are zeroed and the
        // handshake stays in StateConnecting so the next Hello retry can
        // re-derive from scratch.
        bool verified;
        try
        {
            verified = UdpFrameCodec.TryDecodeAuthenticated(
                datagram,
                candidateInbound,
                out header,
                out payload);
        }
        catch
        {
            verified = false;
        }

        if (!verified)
        {
            CryptographicOperations.ZeroMemory(candidateSessionKey);
            CryptographicOperations.ZeroMemory(candidateInbound);
            CryptographicOperations.ZeroMemory(candidateOutbound);
            return false;
        }

        lock (_securitySync)
        {
            if (_sessionKey is not null)
            {
                CryptographicOperations.ZeroMemory(_sessionKey);
            }

            if (_inboundKey is not null)
            {
                CryptographicOperations.ZeroMemory(_inboundKey);
            }

            if (_outboundKey is not null)
            {
                CryptographicOperations.ZeroMemory(_outboundKey);
            }

            _sessionKey = candidateSessionKey;
            _inboundKey = candidateInbound;
            _outboundKey = candidateOutbound;
        }

        return true;
    }

    private void EncodeSessionFrame(
        UdpFrameKind kind,
        ReadOnlySpan<byte> payload,
        Span<byte> destination)
    {
        lock (_securitySync)
        {
            var outboundKey = _outboundKey
                ?? throw new InvalidOperationException("UDP session key is not established.");
            var sequence = checked((ulong)Interlocked.Increment(ref _nextOutboundSequence));
            UdpFrameCodec.EncodeAuthenticated(
                kind,
                new ConnectionId(Volatile.Read(ref _assignedId)),
                sequence,
                payload,
                outboundKey,
                destination);
        }
    }

    /// <summary>Encodes a handshake frame (Hello or WelcomeConfirm) using
    /// sequence 0 and an explicit connection id. Hello uses the PSK as the
    /// MAC key (the session key is not yet derived); WelcomeConfirm uses the
    /// client-to-server direction key derived from the session key. The
    /// caller chooses the key by passing the appropriate span.</summary>
    public void EncodeHandshakeFrame(
        UdpFrameKind kind,
        ConnectionId connectionId,
        ReadOnlySpan<byte> payload,
        Span<byte> destination)
    {
        if (kind == UdpFrameKind.Hello)
        {
            UdpFrameCodec.EncodeAuthenticated(
                kind,
                connectionId,
                sequence: 0UL,
                payload,
                _authenticationKey,
                destination);
            return;
        }

        if (kind == UdpFrameKind.WelcomeConfirm)
        {
            lock (_securitySync)
            {
                var outboundKey = _outboundKey
                    ?? throw new InvalidOperationException(
                        "UDP session key is not established.");
                UdpFrameCodec.EncodeAuthenticated(
                    kind,
                    connectionId,
                    sequence: 0UL,
                    payload,
                    outboundKey,
                    destination);
            }

            return;
        }

        throw new ArgumentOutOfRangeException(
            nameof(kind),
            kind,
            "EncodeHandshakeFrame only supports Hello and WelcomeConfirm.");
    }

    private bool TryAcceptInboundSequence(ulong sequence)
    {
        if (sequence == 0UL)
        {
            return false;
        }

        if (sequence > _highestInboundSequence)
        {
            var advance = sequence - _highestInboundSequence;
            if (advance >= (ulong)ReplayWindowSlots)
            {
                _inboundWindowBitfieldLow = 0UL;
                _inboundWindowBitfieldHigh = 0UL;
            }
            else
            {
                ShiftWindowLeft((int)advance);
            }

            _highestInboundSequence = sequence;
            SetBit(sequence);
            return true;
        }

        var trailingEdge = _highestInboundSequence > (ulong)ReplayWindowSlots
            ? _highestInboundSequence - (ulong)ReplayWindowSlots + 1UL
            : 1UL;
        if (sequence < trailingEdge)
        {
            return false;
        }

        if (IsBitSet(sequence))
        {
            return false;
        }

        SetBit(sequence);
        return true;
    }

    private void ShiftWindowLeft(int shift)
    {
        if (shift <= 0)
        {
            return;
        }

        if (shift >= ReplayWindowSlots)
        {
            _inboundWindowBitfieldLow = 0UL;
            _inboundWindowBitfieldHigh = 0UL;
            return;
        }

        if (shift < 64)
        {
            _inboundWindowBitfieldHigh =
                (_inboundWindowBitfieldHigh << shift)
                | (_inboundWindowBitfieldLow >>> (64 - shift));
            _inboundWindowBitfieldLow <<= shift;
        }
        else
        {
            var secondShift = shift - 64;
            _inboundWindowBitfieldHigh = secondShift == 0
                ? _inboundWindowBitfieldLow
                : _inboundWindowBitfieldLow << secondShift;
            _inboundWindowBitfieldLow = 0UL;
        }
    }

    private void SetBit(ulong sequence)
    {
        var offset = _highestInboundSequence - sequence;
        if (offset >= (ulong)ReplayWindowSlots)
        {
            return;
        }

        if (offset < 64UL)
        {
            _inboundWindowBitfieldLow |= 1UL << (int)offset;
        }
        else
        {
            _inboundWindowBitfieldHigh |= 1UL << (int)(offset - 64UL);
        }
    }

    private bool IsBitSet(ulong sequence)
    {
        var offset = _highestInboundSequence - sequence;
        if (offset >= (ulong)ReplayWindowSlots)
        {
            return false;
        }

        return offset < 64UL
            ? (_inboundWindowBitfieldLow & (1UL << (int)offset)) != 0UL
            : (_inboundWindowBitfieldHigh & (1UL << (int)(offset - 64UL))) != 0UL;
    }

    private void ClearAuthenticationMaterial()
    {
        lock (_securitySync)
        {
            CryptographicOperations.ZeroMemory(_clientNonce);
            if (_sessionKey is not null)
            {
                CryptographicOperations.ZeroMemory(_sessionKey);
                _sessionKey = null;
            }

            if (_inboundKey is not null)
            {
                CryptographicOperations.ZeroMemory(_inboundKey);
                _inboundKey = null;
            }

            if (_outboundKey is not null)
            {
                CryptographicOperations.ZeroMemory(_outboundKey);
                _outboundKey = null;
            }
        }
    }
}
