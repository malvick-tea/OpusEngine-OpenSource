using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Opus.Net.Transport;
using Opus.Net.Udp.Frame;

namespace Opus.Net.Udp.Transport;

/// <summary>
/// Real-socket UDP <see cref="INetTransport"/> for the server side. Binds a UDP socket
/// to a known local endpoint, accepts arbitrarily many client links concurrently, and
/// hands every observed event to game code through <see cref="Poll"/>. The N:1 sibling
/// of <see cref="UdpClientTransport"/>; production counterpart of
/// <see cref="Loopback.LoopbackTransportHub"/>.
/// </summary>
/// <remarks>
/// <para>
/// Connection acceptance is automatic: a fresh <see cref="UdpFrameKind.Hello"/> from an
/// unknown remote endpoint allocates a new <see cref="ConnectionId"/>, queues a
/// <c>Connected</c> event, and ships a <see cref="UdpFrameKind.WelcomeAck"/> back. There
/// is no explicit Accept call — game code only ever needs the surfaced
/// <see cref="NetEvent"/> stream. Repeat Hellos from a known endpoint re-ship the
/// WelcomeAck (handles a lost return-trip handshake) but do not re-emit Connected.
/// </para>
/// <para>
/// Split across partial files per ADR-0029: this file holds the public lifecycle +
/// <see cref="INetTransport"/> surface; <c>UdpServerTransport.Worker.cs</c> owns the
/// receive thread + housekeeping cadence; <c>UdpServerTransport.Dispatch.cs</c> handles
/// per-frame routing + slot bookkeeping + outbound send helpers.
/// </para>
/// </remarks>
public sealed partial class UdpServerTransport : INetTransport, INetServerTransportDiagnostics
{
    private readonly UdpTransportOptions _options;
    private readonly byte[] _authenticationKey;
    private readonly HashSet<IPAddress>? _allowedRemoteAddresses;
    private readonly Socket _socket;
    private readonly Thread _worker;
    private readonly ConcurrentQueue<NetEvent> _inbox = new();
    private readonly byte[] _receiveBuffer = new byte[UdpFrameHeader.MaxDatagramBytes];
    private readonly Dictionary<IPEndPoint, UdpServerPeerSlot> _slotsByEndpoint = new();
    private readonly Dictionary<ConnectionId, UdpServerPeerSlot> _slotsById = new();
    private readonly Dictionary<IPAddress, HelloSourceRateState> _helloSources = new();
    private readonly object _peersLock = new();
    private readonly ILogger _logger;

    private long _rejectedHelloCount;
    private long _inboxCount;
    private long _droppedInboundPayloadCount;
    private long _rateLimitedInboundPayloadCount;
    private int _disposed;

    private UdpServerTransport(
        string name,
        IPEndPoint boundEndpoint,
        Socket socket,
        UdpTransportOptions options,
        ILogger logger)
    {
        Name = name;
        BoundEndpoint = boundEndpoint;
        _socket = socket;
        _authenticationKey = options.AuthenticationKey.ToArray();
        _allowedRemoteAddresses = options.AllowedRemoteAddresses is null
            ? null
            : new HashSet<IPAddress>(options.AllowedRemoteAddresses);
        _options = options with
        {
            AuthenticationKey = ReadOnlyMemory<byte>.Empty,
            AllowedRemoteAddresses = null,
        };
        _logger = logger;
        _worker = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = $"udp-server:{name}",
        };
        try
        {
            _worker.Start();
        }
        catch
        {
            CryptographicOperations.ZeroMemory(_authenticationKey);
            throw;
        }
    }

    public string Name { get; }

    /// <summary>The actual <see cref="IPEndPoint"/> the server is listening on. When the
    /// caller binds with port 0, this surfaces the OS-assigned ephemeral port.</summary>
    public IPEndPoint BoundEndpoint { get; }

    public bool IsOpen => Volatile.Read(ref _disposed) == 0;

    /// <summary>Count of inbound <see cref="UdpFrameKind.Hello"/> frames the server rejected
    /// because the peer table was already at <see cref="UdpTransportOptions.MaxConcurrentPeers"/>.
    /// Observability for the connection-flood guard: a climbing value under load means the cap
    /// is shedding new peers (whether a hostile flood or a genuinely over-subscribed server).
    /// Thread-safe.</summary>
    public long RejectedHelloCount
    {
        get
        {
            lock (_peersLock)
            {
                return _rejectedHelloCount;
            }
        }
    }

    /// <summary>Count of inbound payload events the server dropped because the poll queue was
    /// already at <see cref="UdpTransportOptions.MaxInboundQueuedEvents"/>. Observability for the
    /// per-peer payload-flood guard: a climbing value means a connected peer is sending payloads
    /// faster than the consumer drains <see cref="Poll"/>, and the surplus is being shed to keep
    /// queued memory bounded. Connection-state events are never counted here because they are never
    /// dropped. Thread-safe.</summary>
    public long DroppedInboundPayloadCount => Interlocked.Read(ref _droppedInboundPayloadCount);

    /// <summary>Count of inbound payload events shed by the per-peer inbound rate limiter because the
    /// originating peer exceeded its <see cref="UdpTransportOptions.MaxInboundPayloadBurstPerPeer"/>
    /// burst and <see cref="UdpTransportOptions.InboundPayloadRefillPerSecondPerPeer"/> sustained
    /// rate. Distinct from <see cref="DroppedInboundPayloadCount"/> (the shared-queue cap): this
    /// counts per-peer fairness shedding that fires regardless of how full the global queue is, so a
    /// climbing value isolates a single peer flooding payloads rather than a slow consumer.
    /// Connection-state events are never rate limited. Thread-safe.</summary>
    public long RateLimitedInboundPayloadCount => Interlocked.Read(ref _rateLimitedInboundPayloadCount);

    /// <summary>Maps the transport-neutral <see cref="INetServerTransportDiagnostics"/> connection
    /// -reject counter onto this transport's UDP-specific <see cref="RejectedHelloCount"/>; the other
    /// two diagnostics members are satisfied implicitly by the same-named public properties above.</summary>
    long INetServerTransportDiagnostics.RejectedConnectionCount => RejectedHelloCount;

    /// <summary>Binds a fresh UDP socket to <paramref name="listenEndpoint"/> (use port 0
    /// for an ephemeral) and starts the receive worker. Throws <see cref="SocketException"/>
    /// when the bind fails — typically because the port is in use.</summary>
    public static UdpServerTransport Bind(
        string name,
        IPEndPoint listenEndpoint,
        UdpTransportOptions? options = null,
        ILogger<UdpServerTransport>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(listenEndpoint);
        var resolvedOptions = options ?? UdpTransportOptions.Default;
        resolvedOptions.Validate();

        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        try
        {
            UdpSocketTuning.SuppressIcmpPortUnreachable(socket);
            socket.Bind(listenEndpoint);
            socket.ReceiveTimeout = (int)resolvedOptions.ReceivePollInterval.TotalMilliseconds;
        }
        catch
        {
            socket.Dispose();
            throw;
        }

        var bound = (IPEndPoint)socket.LocalEndPoint!;
        var resolvedLogger = (ILogger?)logger ?? NullLogger.Instance;
        try
        {
            return new UdpServerTransport(name, bound, socket, resolvedOptions, resolvedLogger);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    public bool Send(ConnectionId target, ReadOnlySpan<byte> payload)
    {
        if (!IsOpen)
        {
            return false;
        }

        if (payload.Length > UdpFrameHeader.MaxPayloadBytes)
        {
            return false;
        }

        UdpServerPeerSlot? slot;
        lock (_peersLock)
        {
            if (!_slotsById.TryGetValue(target, out slot) || !slot.IsConnected)
            {
                return false;
            }
        }

        Span<byte> scratch = stackalloc byte[
            UdpFrameHeader.SizeBytes + UdpFrameHeader.AuthenticationTagBytes + 1024];
        var totalBytes = UdpFrameHeader.SizeBytes
            + payload.Length
            + UdpFrameHeader.AuthenticationTagBytes;
        var buffer = totalBytes <= scratch.Length ? scratch[..totalBytes] : new byte[totalBytes];

        if (!TryEncodeSessionFrame(UdpFrameKind.Payload, payload, slot, buffer))
        {
            return false;
        }

        return TrySendBytes(buffer, slot);
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
        UdpServerPeerSlot? slot = null;
        lock (_peersLock)
        {
            if (_slotsById.TryGetValue(target, out slot) && slot.IsConnected)
            {
                slot.IsConnected = false;
            }
            else
            {
                slot = null;
            }
        }

        if (slot is null)
        {
            return;
        }

        SendControlFrame(UdpFrameKind.Disconnect, target, slot);
        EnqueueControlEvent(NetEvent.Disconnected(target));
        RemoveSlot(slot);
    }

    /// <summary>Enqueues a connection-state event. Control events are never shed — losing a
    /// Disconnected would leak a peer perception — so this only tracks the queue depth.</summary>
    private void EnqueueControlEvent(NetEvent netEvent)
    {
        Interlocked.Increment(ref _inboxCount);
        _inbox.Enqueue(netEvent);
    }

    /// <summary>Enqueues an inbound payload event under the bounded-queue backpressure policy:
    /// once the queue is at <see cref="UdpTransportOptions.MaxInboundQueuedEvents"/> the payload is
    /// dropped and counted instead of growing the queue without bound. Only the receive worker
    /// enqueues payloads, so the check-then-increment needs no extra lock; the payload is copied
    /// only when it is actually accepted.</summary>
    private void TryEnqueuePayload(ConnectionId connectionId, ReadOnlySpan<byte> payload)
    {
        if (Interlocked.Read(ref _inboxCount) >= _options.MaxInboundQueuedEvents)
        {
            Interlocked.Increment(ref _droppedInboundPayloadCount);
            return;
        }

        Interlocked.Increment(ref _inboxCount);
        _inbox.Enqueue(NetEvent.Received(connectionId, payload.ToArray()));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        UdpServerPeerSlot[] live;
        lock (_peersLock)
        {
            live = new UdpServerPeerSlot[_slotsById.Count];
            var index = 0;
            foreach (var slot in _slotsById.Values)
            {
                live[index++] = slot;
            }
        }

        foreach (var slot in live)
        {
            if (slot.IsConnected)
            {
                Disconnect(slot.Id);
            }
        }

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

        CryptographicOperations.ZeroMemory(_authenticationKey);
    }
}
