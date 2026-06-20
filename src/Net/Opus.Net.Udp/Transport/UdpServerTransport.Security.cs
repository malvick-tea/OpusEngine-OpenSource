using System;
using System.Net;
using Opus.Net.Transport;
using Opus.Net.Udp.Frame;

namespace Opus.Net.Udp.Transport;

public sealed partial class UdpServerTransport
{
    private bool TryDecodeIncoming(
        IPEndPoint sender,
        ReadOnlySpan<byte> datagram,
        out UdpFrameHeader header,
        out ReadOnlySpan<byte> payload)
    {
        header = default;
        payload = default;

        if (_allowedRemoteAddresses is not null
            && !_allowedRemoteAddresses.Contains(sender.Address))
        {
            return false;
        }

        if (!UdpFrameCodec.TryDecodeEnvelope(datagram, out var envelope, out var untrustedPayload, out _))
        {
            return false;
        }

        if (envelope.Kind == UdpFrameKind.Hello)
        {
            return TryAcceptHello(sender, datagram, untrustedPayload, out header, out payload);
        }

        UdpServerPeerSlot? slot;
        lock (_peersLock)
        {
            if (!_slotsByEndpoint.TryGetValue(sender, out slot)
                || slot.Id != envelope.ConnectionId)
            {
                return false;
            }

            // WelcomeConfirm is the third leg of the handshake: the slot is
            // HalfOpen, the sequence is 0 (handshake frames do not advance
            // the replay window), and the payload is the echoed server
            // challenge. Payload/Heartbeat/Disconnect require a Connected
            // slot and a strictly-monotonic non-zero sequence.
            if (envelope.Kind == UdpFrameKind.WelcomeConfirm)
            {
                if (!slot.IsHalfOpen || envelope.Sequence != 0)
                {
                    return false;
                }
            }
            else if (!slot.IsConnected)
            {
                return false;
            }
        }

        // The slot lock serialises concurrent inbound frames from one peer.
        // MAC verification and the replay-window update must be atomic so an
        // attacker cannot race a fresh sequence past a stale bitfield entry.
        if (!slot.TryDecodeAuthenticated(datagram, out header, out payload))
        {
            return false;
        }

        // WelcomeConfirm uses sequence 0 (handshake); it does not advance the
        // anti-replay window. Every session frame must use a strictly non-
        // zero sequence and pass the sliding-window check.
        if (header.Kind == UdpFrameKind.WelcomeConfirm)
        {
            return header.Sequence == 0;
        }

        if (header.Sequence == 0)
        {
            return false;
        }

        lock (slot.ReplayWindowLock)
        {
            if (!slot.TryAcceptInboundSequence(header.Sequence))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Accepts (or refreshes) a Hello frame. The per-IP rate limiter
    /// runs BEFORE the HMAC verify so an unauthenticated flood cannot force a
    /// SHA-256 per packet — this is the inverse of the original ordering and
    /// closes a pre-auth CPU amplification where a botnet without the PSK
    /// could saturate a core with forced MAC computations. The trade-off is
    /// that a flood with a valid PSK is no longer shed by this limiter, but
    /// that case is already covered by <see cref="UdpTransportOptions.MaxConcurrentPeersPerAddress"/>
    /// on the slot side.</summary>
    private bool TryAcceptHello(
        IPEndPoint sender,
        ReadOnlySpan<byte> datagram,
        ReadOnlySpan<byte> untrustedPayload,
        out UdpFrameHeader header,
        out ReadOnlySpan<byte> payload)
    {
        header = default;
        payload = default;

        if (!AllowHello(sender.Address))
        {
            return false;
        }

        if (untrustedPayload.Length != UdpAuthentication.NonceBytes)
        {
            return false;
        }

        return UdpFrameCodec.TryDecodeAuthenticated(
            datagram,
            _authenticationKey,
            out header,
            out payload);
    }

    private bool AllowHello(IPAddress address)
    {
        var now = Environment.TickCount64;
        lock (_peersLock)
        {
            if (!_helloSources.TryGetValue(address, out var state))
            {
                if (_helloSources.Count >= _options.MaxTrackedHelloSources)
                {
                    _rejectedHelloCount++;
                    return false;
                }

                state = new HelloSourceRateState(
                    new TokenBucketRateLimiter(
                        _options.HelloBurstPerSource,
                        _options.HelloRefillPerSecondPerSource,
                        now),
                    now);
                _helloSources[address] = state;
            }

            state.LastSeenTicks = now;
            if (state.Limiter.TryConsume(now))
            {
                return true;
            }

            _rejectedHelloCount++;
            return false;
        }
    }

    private void SendWelcomeAck(UdpServerPeerSlot slot)
    {
        var totalBytes = UdpFrameHeader.SizeBytes
            + UdpAuthentication.NonceBytes
            + UdpFrameHeader.AuthenticationTagBytes;
        Span<byte> buffer = stackalloc byte[totalBytes];
        if (slot.TryEncodeWelcome(buffer))
        {
            TrySendBytes(buffer, slot);
        }
    }

    private bool TryEncodeSessionFrame(
        UdpFrameKind kind,
        ReadOnlySpan<byte> payload,
        UdpServerPeerSlot slot,
        Span<byte> destination)
    {
        return slot.TryEncodeSessionFrame(kind, payload, destination);
    }

    private sealed class HelloSourceRateState
    {
        public HelloSourceRateState(TokenBucketRateLimiter limiter, long lastSeenTicks)
        {
            Limiter = limiter;
            LastSeenTicks = lastSeenTicks;
        }

        public TokenBucketRateLimiter Limiter { get; }

        public long LastSeenTicks { get; set; }
    }
}
