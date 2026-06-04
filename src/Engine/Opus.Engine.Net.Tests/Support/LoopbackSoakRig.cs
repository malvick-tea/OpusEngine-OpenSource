using System;
using System.Collections.Generic;
using Opus.Engine.Net.Soak;
using Opus.Net.Loopback;
using Opus.Net.Transport;

namespace Opus.Engine.Net.Tests.Support;

/// <summary>Loopback-backed <see cref="INetSoakRig"/>. Wires N client
/// <see cref="LoopbackTransport"/>s to a single <see cref="LoopbackTransportHub"/>; both
/// sides observe Connected events on their first poll. Tests use it to exercise the soak
/// harness without a real UDP socket.</summary>
internal sealed class LoopbackSoakRig : INetSoakRig
{
    private readonly LoopbackTransportHub _hub;
    private readonly List<INetTransport> _clients = new();

    private LoopbackSoakRig(LoopbackTransportHub hub)
    {
        _hub = hub;
    }

    public static LoopbackSoakRig Create(int peerCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(peerCount);
        var hub = LoopbackTransportHub.Create();
        var rig = new LoopbackSoakRig(hub);
        for (var i = 0; i < peerCount; i++)
        {
            rig._clients.Add(hub.Accept($"soak-client-{i}").Client);
        }

        return rig;
    }

    public INetTransport Server => _hub;

    public int PeerCount => _clients.Count;

    public INetTransport Client(int peerIndex) => _clients[peerIndex];

    public ConnectionId ServerSentinel => LoopbackTransportHub.HubSentinelId;

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
