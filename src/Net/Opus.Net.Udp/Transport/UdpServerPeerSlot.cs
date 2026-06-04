using System.Net;
using Opus.Net.Transport;

namespace Opus.Net.Udp.Transport;

/// <summary>
/// Server-side per-peer state held by <see cref="UdpServerTransport"/>. A slot exists
/// from the moment the server allocates a <see cref="ConnectionId"/> in response to a
/// <see cref="Frame.UdpFrameKind.Hello"/> until the connection is torn down (clean
/// Disconnect, dead-peer timeout, or server Dispose).
/// </summary>
internal sealed class UdpServerPeerSlot
{
    public UdpServerPeerSlot(
        ConnectionId id,
        IPEndPoint endpoint,
        long nowTicks,
        TokenBucketRateLimiter inboundRateLimiter)
    {
        Id = id;
        Endpoint = endpoint;
        LastSeenTicks = nowTicks;
        LastSentTicks = nowTicks;
        IsConnected = true;
        InboundRateLimiter = inboundRateLimiter;
    }

    public ConnectionId Id { get; }

    public IPEndPoint Endpoint { get; }

    public long LastSeenTicks { get; set; }

    public long LastSentTicks { get; set; }

    public bool IsConnected { get; set; }

    /// <summary>Per-peer token bucket bounding how fast this peer may enqueue inbound payloads.
    /// Mutated only by the receive worker (see <c>UdpServerTransport.HandlePayload</c>), so it
    /// shares the worker-confined contract of <see cref="LastSeenTicks"/> and needs no lock.</summary>
    public TokenBucketRateLimiter InboundRateLimiter { get; }
}
