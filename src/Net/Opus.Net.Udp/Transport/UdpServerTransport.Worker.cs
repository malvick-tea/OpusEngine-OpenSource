using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Extensions.Logging;
using Opus.Net.Udp.Frame;

namespace Opus.Net.Udp.Transport;

/// <summary>
/// Receive-thread and per-tick housekeeping for <see cref="UdpServerTransport"/>. Split
/// out of the main partial per ADR-0029 — the worker concern is one continuous loop and
/// stays separately readable.
/// </summary>
public sealed partial class UdpServerTransport
{
    private void WorkerLoop()
    {
        EndPoint senderEndpoint = new IPEndPoint(IPAddress.Any, 0);
        while (Volatile.Read(ref _disposed) == 0)
        {
            int receivedBytes;
            try
            {
                receivedBytes = _socket.ReceiveFrom(_receiveBuffer, ref senderEndpoint);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
            {
                Housekeep();
                continue;
            }
            catch (SocketException) when (Volatile.Read(ref _disposed) != 0)
            {
                break;
            }
            catch (SocketException ex) when (UdpSocketTuning.IsTransientReceiveError(ex))
            {
                _logger.LogDebug(ex, "udp server {Name} transient receive error", Name);
                Housekeep();
                continue;
            }
            catch (SocketException ex)
            {
                _logger.LogDebug(ex, "udp server {Name} receive failed; exiting worker", Name);
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            if (senderEndpoint is not IPEndPoint typedSender)
            {
                continue;
            }

            HandleFrame(typedSender, new ReadOnlySpan<byte>(_receiveBuffer, 0, receivedBytes));
            Housekeep();
        }
    }

    private void Housekeep()
    {
        var now = Environment.TickCount64;
        var deadlineMs = _options.DeadlineDuration.TotalMilliseconds;
        var heartbeatMs = _options.HeartbeatInterval.TotalMilliseconds;
        UdpServerPeerSlot[] snapshot;
        lock (_peersLock)
        {
            snapshot = new UdpServerPeerSlot[_slotsById.Count];
            var index = 0;
            foreach (var slot in _slotsById.Values)
            {
                snapshot[index++] = slot;
            }
        }

        foreach (var slot in snapshot)
        {
            if (!slot.IsConnected)
            {
                continue;
            }

            if (now - slot.LastSeenTicks >= deadlineMs)
            {
                TimeOutSlot(slot);
                continue;
            }

            if (now - slot.LastSentTicks >= heartbeatMs)
            {
                SendControlFrame(UdpFrameKind.Heartbeat, slot.Id, slot);
            }
        }
    }
}
