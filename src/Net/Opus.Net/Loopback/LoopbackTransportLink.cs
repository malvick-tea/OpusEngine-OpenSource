using Opus.Net.Transport;

namespace Opus.Net.Loopback;

/// <summary>The wired pair of transports plus the connection ids each side uses to talk
/// to the other. Returned by <see cref="LoopbackTransportPair.Create"/>.</summary>
/// <param name="Client">The client side of the link.</param>
/// <param name="Server">The server side of the link.</param>
/// <param name="ClientPeerId">The id by which the server addresses the client (use it
/// from server-side <see cref="INetTransport.Send"/>).</param>
/// <param name="ServerPeerId">The id by which the client addresses the server (use it
/// from client-side <see cref="INetTransport.Send"/>).</param>
public sealed record LoopbackTransportLink(
    INetTransport Client,
    INetTransport Server,
    ConnectionId ClientPeerId,
    ConnectionId ServerPeerId);
