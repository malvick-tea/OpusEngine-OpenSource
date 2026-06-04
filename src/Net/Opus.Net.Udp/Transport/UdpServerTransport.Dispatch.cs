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
        if (!UdpFrameCodec.TryDecode(datagram, out var header, out var payload))
        {
            return;
        }

        switch (header.Kind)
        {
            case UdpFrameKind.Hello:
                HandleHello(sender);
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

    private void HandleHello(IPEndPoint sender)
    {
        var now = Environment.TickCount64;
        bool isFresh;
        UdpServerPeerSlot slot;
        lock (_peersLock)
        {
            if (_slotsByEndpoint.TryGetValue(sender, out var existing))
            {
                slot = existing;
                isFresh = false;
            }
            else if (_slotsByEndpoint.Count >= _options.MaxConcurrentPeers)
            {
                // Peer table is full. Drop the Hello without allocating a slot, queuing a
                // Connected event, or replying. This is the connection-flood DoS guard: an
                // unauthenticated party flooding Hellos from many source endpoints cannot grow
                // the peer table (or the inbound event queue, which only fills for accepted
                // peers) past the configured cap. Staying silent also avoids turning the
                // server into a WelcomeAck reflector toward a spoofed source address.
                _rejectedHelloCount++;
                return;
            }
            else
            {
                var assignedId = new ConnectionId(_nextPeerCounter++);
                slot = new UdpServerPeerSlot(assignedId, sender, now, NewInboundRateLimiter(now));
                _slotsByEndpoint[sender] = slot;
                _slotsById[assignedId] = slot;
                isFresh = true;
            }
        }

        slot.LastSeenTicks = now;
        SendControlFrame(UdpFrameKind.WelcomeAck, slot.Id, slot);
        if (isFresh)
        {
            EnqueueControlEvent(NetEvent.Connected(slot.Id));
        }
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

    private void RemoveSlot(UdpServerPeerSlot slot)
    {
        lock (_peersLock)
        {
            _slotsByEndpoint.Remove(slot.Endpoint);
            _slotsById.Remove(slot.Id);
        }
    }

    private void SendControlFrame(UdpFrameKind kind, ConnectionId connectionId, UdpServerPeerSlot slot)
    {
        Span<byte> buffer = stackalloc byte[UdpFrameHeader.SizeBytes];
        UdpFrameCodec.Encode(kind, connectionId, ReadOnlySpan<byte>.Empty, buffer);
        TrySendBytes(buffer, slot);
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
