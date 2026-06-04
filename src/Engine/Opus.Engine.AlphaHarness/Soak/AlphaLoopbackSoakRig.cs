using System;
using System.Collections.Generic;
using Opus.Engine.Net.Soak;
using Opus.Net.Loopback;
using Opus.Net.Transport;

namespace Opus.Engine.AlphaHarness.Soak;

/// <summary>
/// Loopback-backed <see cref="INetSoakRig"/> the M9 alpha host CLI uses to drive the
/// existing <see cref="NetSoakHarness"/> without standing up a real UDP socket. Mirrors
/// the test-only rig that lives under <c>Opus.Engine.Net.Tests</c>, exposed here as
/// public runtime code so the alpha host can offer a <c>soak</c> mode without
/// referencing a test assembly.
/// </summary>
public sealed class AlphaLoopbackSoakRig : INetSoakRig
{
    private readonly LoopbackTransportHub _hub;
    private readonly List<INetTransport> _clients = new();

    private AlphaLoopbackSoakRig(LoopbackTransportHub hub)
    {
        _hub = hub;
    }

    /// <summary>Builds a rig with <paramref name="peerCount"/> client transports already
    /// accepted by the hub. The hub itself is owned by the rig and released on
    /// <see cref="Dispose"/>.</summary>
    public static AlphaLoopbackSoakRig Create(int peerCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(peerCount);
        var hub = LoopbackTransportHub.Create();
        var rig = new AlphaLoopbackSoakRig(hub);
        for (var i = 0; i < peerCount; i++)
        {
            rig._clients.Add(hub.Accept($"alpha-soak-client-{i}").Client);
        }

        return rig;
    }

    /// <summary>Server transport seen by the harness.</summary>
    public INetTransport Server => _hub;

    /// <summary>Peer count the rig was constructed with.</summary>
    public int PeerCount => _clients.Count;

    /// <summary>Returns the client transport for the supplied peer.</summary>
    public INetTransport Client(int peerIndex) => _clients[peerIndex];

    /// <summary>Sentinel connection id used by the harness to address the loopback hub.</summary>
    public ConnectionId ServerSentinel => LoopbackTransportHub.HubSentinelId;

    /// <summary>Releases every client transport and the hub.</summary>
    public void Dispose()
    {
        foreach (var client in _clients)
        {
            client.Dispose();
        }

        _clients.Clear();
        _hub.Dispose();
    }
}
