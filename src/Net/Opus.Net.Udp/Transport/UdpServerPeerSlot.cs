using System;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using Opus.Net.Transport;
using Opus.Net.Udp.Frame;

namespace Opus.Net.Udp.Transport;

/// <summary>
/// Server-side per-peer state held by <see cref="UdpServerTransport"/>. A slot exists
/// from the moment the server allocates a <see cref="ConnectionId"/> in response to a
/// <see cref="Frame.UdpFrameKind.Hello"/> until the connection is torn down (clean
/// Disconnect, dead-peer timeout, or server Dispose).
/// </summary>
internal sealed class UdpServerPeerSlot
{
    /// <summary>Width of the anti-replay acceptance window, in sequence
    /// numbers. A frame is accepted when its sequence is greater than the
    /// highest previously seen (<see cref="_highestInboundSequence"/>) or
    /// falls inside the window below it and has not been observed yet. The
    /// window is 64 slots — enough to absorb roughly one second of reordering
    /// on a 60 Hz link — and is implemented as a two-ulong bitfield so the
    /// common path stays branch-free and cache-friendly. Mirrors the RFC 4303
    /// sliding-window anti-replay design.</summary>
    public const int ReplayWindowSlots = 64;

    private readonly object _securitySync = new();
    private readonly object _replayWindowLock = new();
    private readonly byte[] _clientNonce;
    private readonly byte[] _serverNonce;
    private readonly byte[] _sessionKey;
    private readonly byte[] _inboundKey;
    private readonly byte[] _outboundKey;

    private bool _secretsCleared;
    private ulong _highestInboundSequence;
    private ulong _inboundWindowBitfieldLow;
    private ulong _inboundWindowBitfieldHigh;

    public UdpServerPeerSlot(
        ConnectionId id,
        IPEndPoint endpoint,
        long nowTicks,
        TokenBucketRateLimiter inboundRateLimiter,
        byte[] clientNonce,
        byte[] serverNonce,
        byte[] sessionKey,
        bool halfOpen = false)
    {
        Id = id;
        Endpoint = endpoint;
        LastSeenTicks = nowTicks;
        LastSentTicks = nowTicks;
        IsConnected = !halfOpen;
        IsHalfOpen = halfOpen;
        InboundRateLimiter = inboundRateLimiter;
        _clientNonce = clientNonce;
        _serverNonce = serverNonce;
        _sessionKey = sessionKey;
        _inboundKey = UdpAuthentication.DeriveDirectionKey(
            sessionKey, id, UdpAuthentication.Direction.ClientToServer);
        _outboundKey = UdpAuthentication.DeriveDirectionKey(
            sessionKey, id, UdpAuthentication.Direction.ServerToClient);
    }

    public ConnectionId Id { get; }

    public IPEndPoint Endpoint { get; }

    public long LastSeenTicks { get; set; }

    public long LastSentTicks { get; set; }

    public bool IsConnected { get; set; }

    /// <summary>True between Hello and WelcomeConfirm. A HalfOpen slot does
    /// not count toward <see cref="UdpTransportOptions.MaxConcurrentPeers"/>
    /// and does not surface a <c>Connected</c> event — it represents a peer
    /// that has proven possession of the PSK but has not yet proven
    /// possession of the derived session key. The server reaps HalfOpen
    /// slots on a shorter timer (see <c>WelcomeConfirmTimeout</c>) so a
    /// flood of replayed Hellos from many source IPs cannot pin the slot
    /// table for the full 10-second dead-peer window.</summary>
    public bool IsHalfOpen { get; private set; }

    /// <summary>Promotes a HalfOpen slot to Connected. Called once the
    /// server has verified the WelcomeConfirm frame's MAC against the
    /// session key, proving the peer can compute the same key the server
    /// derived from the PSK + nonces. After promotion the slot is eligible
    /// for the dead-peer timeout, counts toward
    /// <see cref="UdpTransportOptions.MaxConcurrentPeers"/>, and the
    /// transport may enqueue the <c>Connected</c> event.</summary>
    public void PromoteToConnected()
    {
        IsHalfOpen = false;
        IsConnected = true;
    }

    /// <summary>Per-peer token bucket bounding how fast this peer may enqueue inbound payloads.
    /// Mutated only by the receive worker (see <c>UdpServerTransport.HandlePayload</c>), so it
    /// shares the worker-confined contract of <see cref="LastSeenTicks"/> and needs no lock.</summary>
    public TokenBucketRateLimiter InboundRateLimiter { get; }

    public ulong LastInboundSequence => _highestInboundSequence;

    /// <summary>Lock object guarding the anti-replay bitfield updates. Held by
    /// the receive worker while <see cref="TryAcceptInboundSequence"/> runs so
    /// two reordered frames from the same peer cannot race past the bitfield.
    /// Kept separate from <see cref="_securitySync"/> so MAC verification
    /// (which can take a SHA-256 pass) does not serialise against replay
    /// bookkeeping for unrelated peers.</summary>
    public object ReplayWindowLock => _replayWindowLock;

    public long NextOutboundSequence;

    public bool MatchesClientNonce(ReadOnlySpan<byte> nonce)
    {
        lock (_securitySync)
        {
            return !_secretsCleared
                && CryptographicOperations.FixedTimeEquals(_clientNonce, nonce);
        }
    }

    /// <summary>Constant-time comparison of the challenge echoed by the
    /// client in <see cref="Frame.UdpFrameKind.WelcomeConfirm"/> against the
    /// server nonce carried in the <see cref="Frame.UdpFrameKind.WelcomeAck"/>
    /// payload. The WelcomeConfirm frame's MAC was already verified against
    /// the inbound direction key by <see cref="TryDecodeAuthenticated"/>, so
    /// reaching this point proves the client derived the same session key.
    /// This check closes a replay where an attacker captures a
    /// WelcomeConfirm from a different (connection_id, server_challenge)
    /// pair and replays it onto a different HalfOpen slot — the echoed
    /// challenge will not match this slot's server nonce.</summary>
    public bool MatchesServerChallenge(ReadOnlySpan<byte> challenge)
    {
        lock (_securitySync)
        {
            return !_secretsCleared
                && CryptographicOperations.FixedTimeEquals(_serverNonce, challenge);
        }
    }

    public bool TryDecodeAuthenticated(
        ReadOnlySpan<byte> datagram,
        out UdpFrameHeader header,
        out ReadOnlySpan<byte> payload)
    {
        lock (_securitySync)
        {
            if (_secretsCleared)
            {
                header = default;
                payload = default;
                return false;
            }

            return UdpFrameCodec.TryDecodeAuthenticated(
                datagram,
                _inboundKey,
                out header,
                out payload);
        }
    }

    public bool TryEncodeWelcome(Span<byte> destination)
    {
        lock (_securitySync)
        {
            if (_secretsCleared)
            {
                return false;
            }

            UdpFrameCodec.EncodeAuthenticated(
                UdpFrameKind.WelcomeAck,
                Id,
                sequence: 0,
                _serverNonce,
                _outboundKey,
                destination);
            return true;
        }
    }

    public bool TryEncodeSessionFrame(
        UdpFrameKind kind,
        ReadOnlySpan<byte> payload,
        Span<byte> destination)
    {
        lock (_securitySync)
        {
            if (_secretsCleared)
            {
                return false;
            }

            var sequence = checked((ulong)Interlocked.Increment(ref NextOutboundSequence));
            UdpFrameCodec.EncodeAuthenticated(
                kind,
                Id,
                sequence,
                payload,
                _outboundKey,
                destination);
            return true;
        }
    }

    /// <summary>Updates the anti-replay window after a frame has been MAC-
    /// verified. Returns true when the sequence is fresh and may be processed;
    /// false when it is a replay (already inside the window) or stale (below
    /// the window). Sequence 0 is reserved for the handshake and is never
    /// accepted here. The caller holds <see cref="_securitySync"/> while
    /// invoking this so the bitfield and high-water mark update atomically
    /// with respect to other inbound frames.</summary>
    public bool TryAcceptInboundSequence(ulong sequence)
    {
        if (sequence == 0UL)
        {
            return false;
        }

        // Fast forward: a brand-new high sequence slides the window forward
        // and clears every older bit. The shift amount is clamped to the
        // window width so a gap-jump of, say, 1000 sequences still leaves a
        // clean zeroed bitfield instead of undefined shift behaviour.
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

        // Stale: below the window's trailing edge. Reject without touching the
        // bitfield so a flood of stale replays cannot corrupt the window.
        var trailingEdge = _highestInboundSequence > (ulong)ReplayWindowSlots
            ? _highestInboundSequence - (ulong)ReplayWindowSlots + 1UL
            : 1UL;
        if (sequence < trailingEdge)
        {
            return false;
        }

        // Inside the window: accept only if the slot is still free.
        if (IsBitSet(sequence))
        {
            return false;
        }

        SetBit(sequence);
        return true;
    }

    private void ShiftWindowLeft(int shift)
    {
        // Treat the two ulongs as one 128-bit field where the low end holds
        // the newest sequences. Shifting "left" by N moves every bit toward
        // the high end so the newest sequence (highest value) lands in slot 0.
        // The high ulong overflows into oblivion; the low ulong pulls in zeros
        // from the bottom. This matches the RFC 4303 sliding window semantics.
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

    public void ClearSecrets()
    {
        lock (_securitySync)
        {
            if (_secretsCleared)
            {
                return;
            }

            _secretsCleared = true;
            CryptographicOperations.ZeroMemory(_clientNonce);
            CryptographicOperations.ZeroMemory(_serverNonce);
            CryptographicOperations.ZeroMemory(_sessionKey);
            CryptographicOperations.ZeroMemory(_inboundKey);
            CryptographicOperations.ZeroMemory(_outboundKey);
        }
    }
}
