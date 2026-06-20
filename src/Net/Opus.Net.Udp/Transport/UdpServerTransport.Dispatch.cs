using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Extensions.Logging;
using Opus.Net.Transport;
using Opus.Net.Udp.Frame;

namespace Opus.Net.Udp.Transport;

/// <summary>
/// Per-frame routing, peer-slot bookkeeping, and outbound send helpers for
/// <see cref="UdpServerTransport"/>. Pulled out of the main partial per ADR-0029 because
/// the dispatch surface is the largest single concern and each handler has a single
/// frame-kind purpose.
/// </summary>
public sealed partial class UdpServerTransport
{
    private void HandleFrame(IPEndPoint sender, ReadOnlySpan<byte> datagram)
    {
        if (!TryDecodeIncoming(sender, datagram, out var header, out var payload))
        {
            return;
        }

        switch (header.Kind)
        {
            case UdpFrameKind.Hello:
                HandleHello(sender, payload);
                break;
            case UdpFrameKind.WelcomeConfirm:
                HandleWelcomeConfirm(sender, header.ConnectionId, payload);
                break;
            case UdpFrameKind.Payload:
                HandlePayload(sender, header.ConnectionId, payload);
                break;
            case UdpFrameKind.Heartbeat:
                RefreshLastSeen(sender, header.ConnectionId);
                break;
            case UdpFrameKind.Disconnect:
                HandleDisconnectFromPeer(sender, header.ConnectionId);
                break;
            default:
                break;
        }
    }

    private void HandleHello(IPEndPoint sender, ReadOnlySpan<byte> clientNonce)
    {
        var now = Environment.TickCount64;
        bool isFresh;
        ConnectionId replacedConnection = ConnectionId.None;
        UdpServerPeerSlot slot;
        lock (_peersLock)
        {
            if (_slotsByEndpoint.TryGetValue(sender, out var existing)
                && existing.MatchesClientNonce(clientNonce))
            {
                // Repeat Hello from a peer whose slot is already in flight.
                // Re-ship the WelcomeAck so a lost return-trip handshake does
                // not strand the client in StateConnecting. Do not emit
                // Connected here — the slot was promoted earlier by
                // HandleWelcomeConfirm, or is still HalfOpen.
                slot = existing;
                isFresh = false;
            }
            else
            {
                if (existing is not null)
                {
                    existing.IsConnected = false;
                    _slotsByEndpoint.Remove(existing.Endpoint);
                    _slotsById.Remove(existing.Id);
                    replacedConnection = existing.Id;
                    existing.ClearSecrets();
                }

                // HalfOpen slots do not count toward MaxConcurrentPeers — a
                // flood of replayed Hellos cannot pin the table for the full
                // dead-peer window. They DO count toward the per-IP cap so a
                // single NAT box cannot carpet-bomb the server. The
                // WelcomeConfirm timer (Housekeep) reaps any HalfOpen slot
                // that did not finish the handshake within
                // WelcomeConfirmTimeout.
                var connectedTotal = 0;
                var connectedFromAddress = 0;
                var halfOpenFromAddress = 0;
                var halfOpenTotal = 0;
                foreach (var activeSlot in _slotsById.Values)
                {
                    if (activeSlot.IsConnected)
                    {
                        connectedTotal++;
                        if (activeSlot.Endpoint.Address.Equals(sender.Address))
                        {
                            connectedFromAddress++;
                        }
                    }
                    else if (activeSlot.IsHalfOpen)
                    {
                        halfOpenTotal++;
                        if (activeSlot.Endpoint.Address.Equals(sender.Address))
                        {
                            halfOpenFromAddress++;
                        }
                    }
                }

                if (connectedTotal >= _options.MaxConcurrentPeers)
                {
                    _rejectedHelloCount++;
                    return;
                }

                if (connectedFromAddress + halfOpenFromAddress
                    >= _options.MaxConcurrentPeersPerAddress)
                {
                    _rejectedHelloCount++;
                    return;
                }

                if (halfOpenTotal >= _options.MaxHalfOpenSlots)
                {
                    _rejectedHelloCount++;
                    return;
                }

                var assignedId = UdpAuthentication.CreateConnectionId(
                    candidate => !_slotsById.ContainsKey(candidate));
                var serverNonce = UdpAuthentication.CreateNonce();
                var clientNonceBytes = clientNonce.ToArray();
                var sessionKey = UdpAuthentication.DeriveSessionKey(
                    _authenticationKey,
                    clientNonceBytes,
                    serverNonce,
                    assignedId);
                slot = new UdpServerPeerSlot(
                    assignedId,
                    sender,
                    now,
                    NewInboundRateLimiter(now),
                    clientNonceBytes,
                    serverNonce,
                    sessionKey,
                    halfOpen: true);
                _slotsByEndpoint[sender] = slot;
                _slotsById[assignedId] = slot;
                isFresh = true;
            }
        }

        if (replacedConnection.IsValid)
        {
            EnqueueControlEvent(NetEvent.Disconnected(replacedConnection));
        }

        slot.LastSeenTicks = now;
        SendWelcomeAck(slot);

        // Do NOT emit Connected here. The slot is HalfOpen; the
        // WelcomeConfirm handler will emit Connected once the peer proves it
        // can MAC with the derived session key. This closes the cross-IP
        // Hello replay vector: a captured Hello is valid from any IP, but
        // the attacker cannot complete the 3-way handshake without the
        // session key, so HalfOpen slots time out without ever counting
        // toward the live peer table.
        if (isFresh && !slot.IsHalfOpen)
        {
            EnqueueControlEvent(NetEvent.Connected(slot.Id));
        }
    }

    /// <summary>Handles the third leg of the 3-way handshake. The client
    /// echoes the server challenge (carried in the WelcomeAck payload)
    /// back, MAC'd with the derived session key. The server verifies the
    /// MAC against the slot's inbound key and promotes the slot from
    /// HalfOpen to Connected. On any failure the slot is torn down — the
    /// peer either forged the frame (no session key) or sent a stale
    /// challenge (replay), both of which are fatal for this
    /// connection.</summary>
    private void HandleWelcomeConfirm(
        IPEndPoint sender,
        ConnectionId headerId,
        ReadOnlySpan<byte> payload)
    {
        if (payload.Length != UdpAuthentication.NonceBytes)
        {
            return;
        }

        UdpServerPeerSlot? slot;
        lock (_peersLock)
        {
            if (!_slotsByEndpoint.TryGetValue(sender, out slot)
                || !slot.IsHalfOpen
                || slot.Id != headerId)
            {
                return;
            }
        }

        // Verify the WelcomeConfirm MAC. The frame was already MAC-verified
        // by TryDecodeIncoming against the inbound (c2s) direction key, so
        // reaching this point proves the client derived the same session
        // key. The remaining check is that the echoed challenge matches the
        // server nonce we sent in WelcomeAck — this closes a replay where
        // an attacker captures a WelcomeConfirm from a different
        // (connection_id, server_challenge) pair and replays it.
        if (!slot.MatchesServerChallenge(payload))
        {
            return;
        }

        lock (_peersLock)
        {
            slot.PromoteToConnected();
        }

        slot.LastSeenTicks = Environment.TickCount64;
        EnqueueControlEvent(NetEvent.Connected(slot.Id));
    }

    private void HandlePayload(IPEndPoint sender, ConnectionId headerId, ReadOnlySpan<byte> payload)
    {
        UdpServerPeerSlot? slot;
        lock (_peersLock)
        {
            if (!_slotsByEndpoint.TryGetValue(sender, out slot) || !slot.IsConnected)
            {
                return;
            }

            if (slot.Id != headerId)
            {
                return;
            }
        }

        var now = Environment.TickCount64;
        slot.LastSeenTicks = now;

        // Per-peer fairness. The global inbox cap in TryEnqueuePayload bounds total queued memory,
        // but on its own it would let one peer fill the whole shared queue and starve the others (and
        // burn the receive worker's decode cost). The per-peer token bucket sheds a single peer's
        // surplus regardless of how full the global queue is, so no one connection monopolises the
        // inbound budget. The peer still counts as alive (LastSeen refreshed above) — it is speaking,
        // just too fast. The bucket is mutated only here, on the single worker thread, so no lock.
        if (!slot.InboundRateLimiter.TryConsume(now))
        {
            Interlocked.Increment(ref _rateLimitedInboundPayloadCount);
            return;
        }

        TryEnqueuePayload(slot.Id, payload);
    }

    private TokenBucketRateLimiter NewInboundRateLimiter(long nowTicks) => new(
        _options.MaxInboundPayloadBurstPerPeer,
        _options.InboundPayloadRefillPerSecondPerPeer,
        nowTicks);

    private void RefreshLastSeen(IPEndPoint sender, ConnectionId headerId)
    {
        lock (_peersLock)
        {
            if (_slotsByEndpoint.TryGetValue(sender, out var slot) && slot.IsConnected && slot.Id == headerId)
            {
                slot.LastSeenTicks = Environment.TickCount64;
            }
        }
    }

    private void HandleDisconnectFromPeer(IPEndPoint sender, ConnectionId headerId)
    {
        UdpServerPeerSlot? slot;
        lock (_peersLock)
        {
            if (!_slotsByEndpoint.TryGetValue(sender, out slot) || !slot.IsConnected || slot.Id != headerId)
            {
                return;
            }

            slot.IsConnected = false;
        }

        EnqueueControlEvent(NetEvent.Disconnected(slot.Id));
        RemoveSlot(slot);
    }

    private void TimeOutSlot(UdpServerPeerSlot slot)
    {
        lock (_peersLock)
        {
            if (!slot.IsConnected)
            {
                return;
            }

            slot.IsConnected = false;
        }

        EnqueueControlEvent(NetEvent.Disconnected(slot.Id));
        RemoveSlot(slot);
    }

    /// <summary>Silently removes a HalfOpen slot whose WelcomeConfirm timeout
    /// expired. No <c>Disconnected</c> event is emitted — the slot never
    /// surfaced <c>Connected</c>, so the game layer never knew about it.
    /// The slot's secrets are zeroed and the endpoint / id maps are
    /// cleaned so a future Hello from the same endpoint can allocate a
    /// fresh slot without colliding with the dead entry.</summary>
    private void ReapHalfOpenSlot(UdpServerPeerSlot slot)
    {
        lock (_peersLock)
        {
            if (!slot.IsHalfOpen)
            {
                return;
            }
        }

        RemoveSlot(slot);
    }

    private void RemoveSlot(UdpServerPeerSlot slot)
    {
        lock (_peersLock)
        {
            _slotsByEndpoint.Remove(slot.Endpoint);
            _slotsById.Remove(slot.Id);
        }

        slot.ClearSecrets();
    }

    private void SendControlFrame(UdpFrameKind kind, ConnectionId connectionId, UdpServerPeerSlot slot)
    {
        Span<byte> buffer = stackalloc byte[
            UdpFrameHeader.SizeBytes + UdpFrameHeader.AuthenticationTagBytes];
        if (TryEncodeSessionFrame(kind, ReadOnlySpan<byte>.Empty, slot, buffer))
        {
            TrySendBytes(buffer, slot);
        }
    }

    private bool TrySendBytes(ReadOnlySpan<byte> buffer, UdpServerPeerSlot slot)
    {
        try
        {
            _socket.SendTo(buffer, SocketFlags.None, slot.Endpoint);
            slot.LastSentTicks = Environment.TickCount64;
            return true;
        }
        catch (SocketException ex)
        {
            _logger.LogDebug(ex, "udp server {Name} send to {Peer} failed", Name, slot.Endpoint);
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }
}
