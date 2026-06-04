using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Opus.Net.Transport;
using Opus.Net.Udp.Frame;

namespace Opus.Net.Udp.Transport;

/// <summary>
/// Real-socket UDP <see cref="INetTransport"/> for the client side of a 1:1 link. Talks
/// to a single <see cref="UdpServerTransport"/> reachable at a known <see cref="IPEndPoint"/>;
/// no NAT punchthrough, no relay — direct UDP to the server.
/// </summary>
/// <remarks>
/// <para>
/// Lifecycle mirrors <see cref="Loopback.LoopbackTransport"/>: construction kicks off the
/// connection, <see cref="Poll"/> drains events on the calling thread, <see cref="Disconnect"/>
/// closes the link bilaterally. The handshake (<see cref="UdpFrameKind.Hello"/> →
/// <see cref="UdpFrameKind.WelcomeAck"/>) and the heartbeat / dead-peer timeout run on the
/// receive worker, so game-tick code never blocks on socket I/O.
/// </para>
/// <para>
/// The sentinel <see cref="ServerSentinelId"/> is the only valid <see cref="ConnectionId"/>
/// a caller passes to <see cref="Send"/> — the client has exactly one peer (the server).
/// Echoes the loopback hub's <c>HubSentinelId</c> design so consumer code is uniform.
/// </para>
/// </remarks>
public sealed class UdpClientTransport : INetTransport
{
    /// <summary>The address the client uses to talk to its single peer (the server). The
    /// real <see cref="ConnectionId"/> the server assigned via WelcomeAck is held inside
    /// the transport and is not surfaced — game code addresses the server symbolically.</summary>
    public static readonly ConnectionId ServerSentinelId = new(ulong.MaxValue);

    private const int StateConnecting = 0;
    private const int StateConnected = 1;
    private const int StateClosed = 2;

    private readonly IPEndPoint _serverEndpoint;
    private readonly UdpTransportOptions _options;
    private readonly Socket _socket;
    private readonly Thread _worker;
    private readonly ConcurrentQueue<NetEvent> _inbox = new();
    private readonly byte[] _receiveBuffer = new byte[UdpFrameHeader.MaxDatagramBytes];
    private readonly ILogger _logger;

    private ulong _assignedId;
    private int _state;
    private long _lastSentTicks;
    private long _lastSeenTicks;
    private long _connectStartedTicks;
    private long _inboxCount;
    private long _droppedInboundPayloadCount;
    private int _disposed;

    public UdpClientTransport(
        string name,
        IPEndPoint serverEndpoint,
        UdpTransportOptions? options = null,
        ILogger<UdpClientTransport>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(serverEndpoint);
        Name = name;
        _serverEndpoint = serverEndpoint;
        _options = options ?? UdpTransportOptions.Default;
        _options.Validate();

        _logger = (ILogger?)logger ?? NullLogger.Instance;

        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        UdpSocketTuning.SuppressIcmpPortUnreachable(_socket);
        _socket.Bind(new IPEndPoint(IPAddress.Any, 0));
        _socket.ReceiveTimeout = (int)_options.ReceivePollInterval.TotalMilliseconds;

        var now = Environment.TickCount64;
        _lastSeenTicks = now;
        _lastSentTicks = now;
        _connectStartedTicks = now;
        _state = StateConnecting;

        SendControlFrame(UdpFrameKind.Hello, connectionId: 0UL);

        _worker = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = $"udp-client:{name}",
        };
        _worker.Start();
    }

    public string Name { get; }

    public bool IsOpen =>
        Volatile.Read(ref _disposed) == 0 && Volatile.Read(ref _state) != StateClosed;

    /// <summary>Count of inbound payload events dropped because the poll queue was already at
    /// <see cref="UdpTransportOptions.MaxInboundQueuedEvents"/>. A climbing value means the server
    /// is sending payloads faster than the consumer drains <see cref="Poll"/>, and the surplus is
    /// shed to keep queued memory bounded. Connection-state events are never dropped. Thread-safe.</summary>
    public long DroppedInboundPayloadCount => Interlocked.Read(ref _droppedInboundPayloadCount);

    public bool Send(ConnectionId target, ReadOnlySpan<byte> payload)
    {
        if (!IsOpen || target != ServerSentinelId)
        {
            return false;
        }

        if (Volatile.Read(ref _state) != StateConnected)
        {
            return false;
        }

        if (payload.Length > UdpFrameHeader.MaxPayloadBytes)
        {
            return false;
        }

        Span<byte> scratch = stackalloc byte[UdpFrameHeader.SizeBytes + 1024];
        var totalBytes = UdpFrameHeader.SizeBytes + payload.Length;
        var buffer = totalBytes <= scratch.Length ? scratch[..totalBytes] : new byte[totalBytes];

        UdpFrameCodec.Encode(UdpFrameKind.Payload, new ConnectionId(_assignedId), payload, buffer);
        return TrySendBytes(buffer);
    }

    public void Poll(List<NetEvent> into)
    {
        ArgumentNullException.ThrowIfNull(into);
        into.Clear();
        while (_inbox.TryDequeue(out var ev))
        {
            Interlocked.Decrement(ref _inboxCount);
            into.Add(ev);
        }
    }

    public void Disconnect(ConnectionId target)
    {
        if (target != ServerSentinelId)
        {
            return;
        }

        TearDown(notifyPeer: true, surfaceDisconnect: true);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        TearDown(notifyPeer: true, surfaceDisconnect: true);

        try
        {
            _socket.Close();
        }
        catch (ObjectDisposedException)
        {
        }

        if (!ReferenceEquals(Thread.CurrentThread, _worker))
        {
            _worker.Join(TimeSpan.FromSeconds(1));
        }
    }

    private void WorkerLoop()
    {
        EndPoint senderEndpoint = new IPEndPoint(IPAddress.Any, 0);
        while (Volatile.Read(ref _disposed) == 0 && Volatile.Read(ref _state) != StateClosed)
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
            catch (SocketException) when (IsShutdownInProgress())
            {
                break;
            }
            catch (SocketException ex) when (UdpSocketTuning.IsTransientReceiveError(ex))
            {
                _logger.LogDebug(ex, "udp client {Name} transient receive error", Name);
                Housekeep();
                continue;
            }
            catch (SocketException ex)
            {
                _logger.LogDebug(ex, "udp client {Name} receive failed; tearing down", Name);
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

            HandleFrame(new ReadOnlySpan<byte>(_receiveBuffer, 0, receivedBytes));
            Housekeep();
        }
    }

    private bool IsShutdownInProgress() =>
        Volatile.Read(ref _disposed) != 0 || Volatile.Read(ref _state) == StateClosed;

    private void HandleFrame(ReadOnlySpan<byte> datagram)
    {
        if (!UdpFrameCodec.TryDecode(datagram, out var header, out var payload))
        {
            return;
        }

        Volatile.Write(ref _lastSeenTicks, Environment.TickCount64);

        switch (header.Kind)
        {
            case UdpFrameKind.WelcomeAck:
                AcceptWelcomeAck(header.ConnectionId);
                break;
            case UdpFrameKind.Payload:
                AcceptPayload(header.ConnectionId, payload);
                break;
            case UdpFrameKind.Heartbeat:
                // The lastSeen refresh above is the only side-effect we need.
                break;
            case UdpFrameKind.Disconnect:
                TearDown(notifyPeer: false, surfaceDisconnect: true);
                break;
            default:
                break;
        }
    }

    private void AcceptWelcomeAck(ConnectionId assigned)
    {
        if (Interlocked.CompareExchange(ref _state, StateConnected, StateConnecting) != StateConnecting)
        {
            return;
        }

        Volatile.Write(ref _assignedId, assigned.Value);
        EnqueueControlEvent(NetEvent.Connected(ServerSentinelId));
    }

    private void AcceptPayload(ConnectionId headerId, ReadOnlySpan<byte> payload)
    {
        if (Volatile.Read(ref _state) != StateConnected)
        {
            return;
        }

        if (headerId.Value != Volatile.Read(ref _assignedId))
        {
            return;
        }

        TryEnqueuePayload(payload);
    }

    /// <summary>Enqueues a connection-state event. Control events are never shed so a Disconnected
    /// is never lost behind a payload flood; this only tracks the queue depth.</summary>
    private void EnqueueControlEvent(NetEvent netEvent)
    {
        Interlocked.Increment(ref _inboxCount);
        _inbox.Enqueue(netEvent);
    }

    /// <summary>Enqueues an inbound payload under the bounded-queue backpressure policy: once the
    /// queue is at <see cref="UdpTransportOptions.MaxInboundQueuedEvents"/> the payload is dropped
    /// and counted instead of growing the queue without bound. Only the receive worker enqueues
    /// payloads, so the check-then-increment needs no extra lock; the payload is copied only when
    /// it is actually accepted.</summary>
    private void TryEnqueuePayload(ReadOnlySpan<byte> payload)
    {
        if (Interlocked.Read(ref _inboxCount) >= _options.MaxInboundQueuedEvents)
        {
            Interlocked.Increment(ref _droppedInboundPayloadCount);
            return;
        }

        Interlocked.Increment(ref _inboxCount);
        _inbox.Enqueue(NetEvent.Received(ServerSentinelId, payload.ToArray()));
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
            var elapsedSinceConnectStart = now - Volatile.Read(ref _connectStartedTicks);
            if (elapsedSinceConnectStart >= _options.ConnectTimeout.TotalMilliseconds)
            {
                TearDown(notifyPeer: false, surfaceDisconnect: true);
                return;
            }

            if (elapsedSinceSend >= _options.HeartbeatInterval.TotalMilliseconds)
            {
                SendControlFrame(UdpFrameKind.Hello, connectionId: 0UL);
            }

            return;
        }

        if (elapsedSinceSeen >= _options.DeadlineDuration.TotalMilliseconds)
        {
            TearDown(notifyPeer: false, surfaceDisconnect: true);
            return;
        }

        if (elapsedSinceSend >= _options.HeartbeatInterval.TotalMilliseconds)
        {
            SendControlFrame(UdpFrameKind.Heartbeat, Volatile.Read(ref _assignedId));
        }
    }

    private void TearDown(bool notifyPeer, bool surfaceDisconnect)
    {
        if (Interlocked.Exchange(ref _state, StateClosed) == StateClosed)
        {
            return;
        }

        if (notifyPeer)
        {
            SendControlFrame(UdpFrameKind.Disconnect, Volatile.Read(ref _assignedId));
        }

        if (surfaceDisconnect)
        {
            EnqueueControlEvent(NetEvent.Disconnected(ServerSentinelId));
        }
    }

    private void SendControlFrame(UdpFrameKind kind, ulong connectionId)
    {
        Span<byte> buffer = stackalloc byte[UdpFrameHeader.SizeBytes];
        UdpFrameCodec.Encode(kind, new ConnectionId(connectionId), ReadOnlySpan<byte>.Empty, buffer);
        TrySendBytes(buffer);
    }

    private bool TrySendBytes(ReadOnlySpan<byte> buffer)
    {
        try
        {
            _socket.SendTo(buffer, SocketFlags.None, _serverEndpoint);
            Volatile.Write(ref _lastSentTicks, Environment.TickCount64);
            return true;
        }
        catch (SocketException ex)
        {
            _logger.LogDebug(ex, "udp client {Name} send failed", Name);
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }
}
