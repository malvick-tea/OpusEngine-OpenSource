using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Extensions.Logging;
using Opus.Net.Transport;
using Opus.Net.Udp.Frame;

namespace Opus.Net.Udp.Transport;

public sealed partial class UdpClientTransport
{
    private void WorkerLoop()
    {
        EndPoint senderEndpoint = new IPEndPoint(IPAddress.Any, 0);
        while (Volatile.Read(ref _disposed) == 0
               && Volatile.Read(ref _state) != StateClosed)
        {
            int receivedBytes;
            try
            {
                receivedBytes = _socket.ReceiveFrom(
                    _receiveBuffer,
                    ref senderEndpoint);
            }
            catch (SocketException ex)
                when (ex.SocketErrorCode == SocketError.TimedOut)
            {
                Housekeep();
                continue;
            }
            catch (SocketException) when (IsShutdownInProgress())
            {
                break;
            }
            catch (SocketException ex)
                when (UdpSocketTuning.IsTransientReceiveError(ex))
            {
                _logger.LogDebug(
                    ex,
                    "udp client {Name} transient receive error",
                    Name);
                Housekeep();
                continue;
            }
            catch (SocketException ex)
            {
                _logger.LogDebug(
                    ex,
                    "udp client {Name} receive failed; tearing down",
                    Name);
                TearDown(notifyPeer: false, surfaceDisconnect: true);
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            if (!senderEndpoint.Equals(_serverEndpoint))
            {
                continue;
            }

            HandleFrame(new ReadOnlySpan<byte>(
                _receiveBuffer,
                0,
                receivedBytes));
            Housekeep();
        }
    }

    private bool IsShutdownInProgress() =>
        Volatile.Read(ref _disposed) != 0
        || Volatile.Read(ref _state) == StateClosed;

    private void HandleFrame(ReadOnlySpan<byte> datagram)
    {
        if (!TryDecodeInbound(datagram, out var header, out var payload))
        {
            return;
        }

        Volatile.Write(ref _lastSeenTicks, Environment.TickCount64);
        switch (header.Kind)
        {
            case UdpFrameKind.WelcomeAck:
                AcceptWelcomeAck(header.ConnectionId, payload);
                break;
            case UdpFrameKind.Payload:
                AcceptPayload(header.ConnectionId, payload);
                break;
            case UdpFrameKind.Heartbeat:
                break;
            case UdpFrameKind.Disconnect:
                TearDown(notifyPeer: false, surfaceDisconnect: true);
                break;
        }
    }

    private void AcceptWelcomeAck(ConnectionId assigned, ReadOnlySpan<byte> serverChallenge)
    {
        if (Volatile.Read(ref _state) != StateConnecting)
        {
            return;
        }

        if (serverChallenge.Length != UdpAuthentication.NonceBytes)
        {
            // The codec already validated the envelope shape, so a wrong-size
            // challenge here means the server is misbehaving or the wire was
            // truncated mid-payload. Tear down so the caller surfaces a
            // Disconnected event instead of looping forever on a half-handshake.
            TearDown(notifyPeer: false, surfaceDisconnect: true);
            return;
        }

        // Publish the assigned connection id BEFORE the state transition. The
        // CAS below is a full barrier, so any thread that observes
        // StateConnected is guaranteed to also observe the assigned id. The
        // previous order (CAS-then-write) was safe on x86/x64 but allowed a
        // narrow window on ARM64 where a Send() on a freshly connected client
        // read _state==Connected with _assignedId==0, encoded the first
        // Payload frame with ConnectionId.None, and silently lost it when the
        // server rejected the slot lookup.
        Volatile.Write(ref _assignedId, assigned.Value);

        // Send the WelcomeConfirm BEFORE the state CAS. The server keeps the
        // slot HalfOpen until this frame arrives, so completing the 3-way
        // handshake must precede any signal that the connection is ready for
        // game-layer traffic. If the send fails (socket closed, network down)
        // we tear down rather than enter StateConnected on a slot the server
        // will reap.
        if (!SendWelcomeConfirm(assigned, serverChallenge))
        {
            Volatile.Write(ref _assignedId, 0UL);
            TearDown(notifyPeer: false, surfaceDisconnect: true);
            return;
        }

        if (Interlocked.CompareExchange(
                ref _state,
                StateConnected,
                StateConnecting) != StateConnecting)
        {
            // Another thread tore the connection down between the state read
            // above and the CAS. Roll the id back so a stale Send does not
            // reference a half-published value.
            Volatile.Write(ref _assignedId, 0UL);
            return;
        }

        EnqueueControlEvent(NetEvent.Connected(ServerSentinelId));
    }

    /// <summary>Encodes and sends the third leg of the 3-way handshake. The
    /// payload is the server challenge echoed verbatim (so the server can
    /// constant-time compare it against the nonce it sent in WelcomeAck),
    /// MAC'd with the client-to-server direction key. Sequence 0 is used
    /// because this is a handshake frame, not a session frame — the
    /// anti-replay window starts counting from the first Payload.</summary>
    private bool SendWelcomeConfirm(ConnectionId assigned, ReadOnlySpan<byte> serverChallenge)
    {
        var totalBytes = UdpFrameHeader.SizeBytes
            + UdpAuthentication.NonceBytes
            + UdpFrameHeader.AuthenticationTagBytes;
        Span<byte> buffer = stackalloc byte[totalBytes];
        try
        {
            EncodeHandshakeFrame(
                UdpFrameKind.WelcomeConfirm,
                assigned,
                serverChallenge,
                buffer);
            return TrySendBytes(buffer);
        }
        catch (InvalidOperationException)
        {
            // EncodeHandshakeFrame throws when the outbound key is not yet
            // established — which can only happen if TryAcceptWelcome's
            // key-derivation failed silently. Treat as a fatal handshake
            // error so the caller tears down.
            return false;
        }
    }

    private void AcceptPayload(
        ConnectionId headerId,
        ReadOnlySpan<byte> payload)
    {
        if (Volatile.Read(ref _state) != StateConnected
            || headerId.Value != Volatile.Read(ref _assignedId))
        {
            return;
        }

        TryEnqueuePayload(payload);
    }

    private void EnqueueControlEvent(NetEvent netEvent)
    {
        Interlocked.Increment(ref _inboxCount);
        _inbox.Enqueue(netEvent);
    }

    private void TryEnqueuePayload(ReadOnlySpan<byte> payload)
    {
        if (Interlocked.Read(ref _inboxCount)
            >= _options.MaxInboundQueuedEvents)
        {
            Interlocked.Increment(ref _droppedInboundPayloadCount);
            return;
        }

        Interlocked.Increment(ref _inboxCount);
        _inbox.Enqueue(NetEvent.Received(
            ServerSentinelId,
            payload.ToArray()));
    }

    private void Housekeep()
    {
        var now = Environment.TickCount64;
        var state = Volatile.Read(ref _state);
        if (state == StateClosed)
        {
            return;
        }

        var elapsedSinceSend = now - Volatile.Read(ref _lastSentTicks);
        var elapsedSinceSeen = now - Volatile.Read(ref _lastSeenTicks);
        if (state == StateConnecting)
        {
            var elapsedSinceConnectStart =
                now - Volatile.Read(ref _connectStartedTicks);
            if (elapsedSinceConnectStart
                >= _options.ConnectTimeout.TotalMilliseconds)
            {
                TearDown(notifyPeer: false, surfaceDisconnect: true);
                return;
            }

            if (elapsedSinceSend
                >= _options.HeartbeatInterval.TotalMilliseconds)
            {
                SendControlFrame(
                    UdpFrameKind.Hello,
                    connectionId: 0UL);
            }

            return;
        }

        if (elapsedSinceSeen >= _options.DeadlineDuration.TotalMilliseconds)
        {
            TearDown(notifyPeer: false, surfaceDisconnect: true);
            return;
        }

        if (elapsedSinceSend
            >= _options.HeartbeatInterval.TotalMilliseconds)
        {
            SendControlFrame(
                UdpFrameKind.Heartbeat,
                Volatile.Read(ref _assignedId));
        }
    }
}
