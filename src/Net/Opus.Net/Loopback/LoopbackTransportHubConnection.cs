using Opus.Net.Transport;

namespace Opus.Net.Loopback;

/// <summary>The result of one <see cref="LoopbackTransportHub.Accept"/> call.
/// <param name="Client">The client-side <see cref="INetTransport"/> the accepted peer
/// uses. Owns its own inbox; <c>Send</c>/<c>Poll</c>/<c>Disconnect</c> talk to the hub
/// transparently. Caller (test, future client harness) owns the <c>Dispose</c>
/// lifetime.</param>
/// <param name="ServerSidePeerId">The <see cref="ConnectionId"/> the hub uses to
/// address this client. Surfaced in the hub's <see cref="NetEventKind.Connected"/> /
/// <see cref="NetEventKind.Received"/> events; required by the hub's <c>Send(peerId, …)</c>
/// API to reach this specific peer.</param>
/// </summary>
public sealed record LoopbackTransportHubConnection(
    INetTransport Client,
    ConnectionId ServerSidePeerId);
