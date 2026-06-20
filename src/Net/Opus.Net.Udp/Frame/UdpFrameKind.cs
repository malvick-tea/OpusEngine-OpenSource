namespace Opus.Net.Udp.Frame;

/// <summary>
/// Discriminator for the seven datagram kinds the UDP transport recognises. Every wire
/// frame begins with a <see cref="UdpFrameHeader"/> whose <see cref="UdpFrameHeader.Kind"/>
/// field carries one of these values; unknown values are dropped by the receiver.
/// </summary>
public enum UdpFrameKind : byte
{
    /// <summary>Reserved sentinel — never written on the wire. Acts as a parse-rejection
    /// marker when a corrupt byte stream produces an out-of-range discriminator.</summary>
    Invalid = 0,

    /// <summary>Client → server. Connection handshake start. Sent with
    /// <see cref="UdpFrameHeader.ConnectionId"/> = 0; the server allocates a real id and
    /// returns it via <see cref="WelcomeAck"/>.</summary>
    Hello = 1,

    /// <summary>Server → client. Confirms the handshake and assigns the connection id the
    /// client must echo on every subsequent frame. The payload carries a 32-byte server
    /// challenge that the client must MAC back in <see cref="WelcomeConfirm"/> before the
    /// slot is promoted from HalfOpen to Connected.</summary>
    WelcomeAck = 2,

    /// <summary>Either direction. Game-layer datagram — the payload bytes are routed
    /// onward as <c>NetEvent.Received</c>.</summary>
    Payload = 3,

    /// <summary>Either direction. Liveness probe — sent when no other traffic has gone
    /// out within <c>HeartbeatInterval</c>. Carries no payload; receipt simply refreshes
    /// the peer's last-seen timestamp.</summary>
    Heartbeat = 4,

    /// <summary>Either direction. Clean close signal. The receiver enqueues a
    /// <c>NetEvent.Disconnected</c> and the slot is destroyed.</summary>
    Disconnect = 5,

    /// <summary>Client → server. Third leg of the 3-way handshake. Sent in response to
    /// <see cref="WelcomeAck"/> with the same connection id and a 32-byte echo of the
    /// server challenge, MAC'd with the derived session key. The server keeps the slot
    /// HalfOpen (does not count toward <c>MaxConcurrentPeers</c>, does not enqueue
    /// <c>Connected</c>) until this frame verifies — closing the cross-IP Hello replay
    /// vector where an attacker with the PSK but without the session key could fill the
    /// slot table by replaying a captured Hello from many source IPs.</summary>
    WelcomeConfirm = 6,
}
