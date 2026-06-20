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
public sealed partial class UdpClientTransport : INetTransport
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
    private readonly byte[] _authenticationKey;
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
        var resolvedOptions = options ?? UdpTransportOptions.Default;
        resolvedOptions.Validate();
        _authenticationKey = resolvedOptions.AuthenticationKey.ToArray();
        _options = resolvedOptions with
        {
            AuthenticationKey = ReadOnlyMemory<byte>.Empty,
            AllowedRemoteAddresses = null,
        };

        _logger = (ILogger?)logger ?? NullLogger.Instance;

        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        try
        {
            UdpSocketTuning.SuppressIcmpPortUnreachable(_socket);
            _socket.Bind(new IPEndPoint(IPAddress.Any, 0));
            _socket.ReceiveTimeout = (int)_options.ReceivePollInterval.TotalMilliseconds;

            var now = Environment.TickCount64;
            _lastSeenTicks = now;
            _lastSentTicks = now;
            _connectStartedTicks = now;
            _state = StateConnecting;

            _worker = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = $"udp-client:{name}",
            };
            if (!SendControlFrame(UdpFrameKind.Hello, connectionId: 0UL))
            {
                throw new SocketException((int)SocketError.NetworkDown);
            }

            _worker.Start();
        }
        catch
        {
            CryptographicOperations.ZeroMemory(_authenticationKey);
            _socket.Dispose();
            throw;
        }
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

        Span<byte> scratch = stackalloc byte[
            UdpFrameHeader.SizeBytes + UdpFrameHeader.AuthenticationTagBytes + 1024];
        var totalBytes = UdpFrameHeader.SizeBytes
            + payload.Length
            + UdpFrameHeader.AuthenticationTagBytes;
        var buffer = totalBytes <= scratch.Length ? scratch[..totalBytes] : new byte[totalBytes];

        EncodeSessionFrame(UdpFrameKind.Payload, payload, buffer);
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

        ClearAuthenticationMaterial();
        CryptographicOperations.ZeroMemory(_authenticationKey);
    }

    private void TearDown(bool notifyPeer, bool surfaceDisconnect)
    {
        var priorState = Interlocked.Exchange(ref _state, StateClosed);
        if (priorState == StateClosed)
        {
            return;
        }

        if (notifyPeer && priorState == StateConnected)
        {
            SendControlFrame(UdpFrameKind.Disconnect, Volatile.Read(ref _assignedId));
        }

        if (surfaceDisconnect)
        {
            EnqueueControlEvent(NetEvent.Disconnected(ServerSentinelId));
        }
    }

    private bool SendControlFrame(UdpFrameKind kind, ulong connectionId)
    {
        if (kind == UdpFrameKind.Hello)
        {
            var totalBytes = UdpFrameHeader.SizeBytes
                + UdpAuthentication.NonceBytes
                + UdpFrameHeader.AuthenticationTagBytes;
            Span<byte> hello = stackalloc byte[totalBytes];
            UdpFrameCodec.EncodeAuthenticated(
                kind,
                ConnectionId.None,
                sequence: 0,
                _clientNonce,
                _authenticationKey,
                hello);
            return TrySendBytes(hello);
        }

        Span<byte> buffer = stackalloc byte[
            UdpFrameHeader.SizeBytes + UdpFrameHeader.AuthenticationTagBytes];
        EncodeSessionFrame(kind, ReadOnlySpan<byte>.Empty, buffer);
        return TrySendBytes(buffer);
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
