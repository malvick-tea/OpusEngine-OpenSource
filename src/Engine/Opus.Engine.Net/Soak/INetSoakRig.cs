using System;
using Opus.Net.Transport;

namespace Opus.Engine.Net.Soak;

/// <summary>
/// Test / harness rig that exposes one bound server transport and a configured number of
/// client transports. Implementations live above transport assemblies (loopback,
/// real UDP) so the soak harness body stays transport-agnostic. Disposing the rig is
/// expected to dispose every owned transport in topological order.
/// </summary>
public interface INetSoakRig : IDisposable
{
    /// <summary>Server-side transport. Receives every workload datagram and (when the
    /// profile asks) echoes it back.</summary>
    INetTransport Server { get; }

    /// <summary>Number of client transports the rig exposes via <see cref="Client"/>.</summary>
    int PeerCount { get; }

    /// <summary>Returns the client-side transport for the given zero-based peer index.</summary>
    INetTransport Client(int peerIndex);

    /// <summary>Sentinel <see cref="ConnectionId"/> client transports use to address the
    /// server on <see cref="INetTransport.Send"/>. By convention both the loopback hub
    /// and the real UDP client use <c>new(ulong.MaxValue)</c> so consumers stay
    /// uniform — the rig still surfaces it as an explicit value rather than relying on a
    /// shared constant.</summary>
    ConnectionId ServerSentinel { get; }
}
