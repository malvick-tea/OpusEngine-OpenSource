using System;
using System.Collections.Generic;
using System.Net;
using Opus.Engine.Net.Soak;
using Opus.Net.Transport;
using Opus.Net.Udp.Transport;

namespace Opus.Engine.Net.Tests.Support;

/// <summary>Real-UDP backed <see cref="INetSoakRig"/>. Spins up a single
/// <see cref="UdpServerTransport"/> bound to <c>127.0.0.1:0</c> plus N
/// <see cref="UdpClientTransport"/>s pointed at the bound endpoint. Used for the M8
/// UDP integration smoke that proves the soak harness drains over a real socket.</summary>
internal sealed class UdpSoakRig : INetSoakRig
{
    private readonly UdpServerTransport _server;
    private readonly List<INetTransport> _clients = new();

    private UdpSoakRig(UdpServerTransport server)
    {
        _server = server;
    }

    public static UdpSoakRig Create(int peerCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(peerCount);
        var options = new UdpTransportOptions
        {
            HeartbeatInterval = TimeSpan.FromMilliseconds(100),
            DeadlineDuration = TimeSpan.FromMilliseconds(1500),
            ReceivePollInterval = TimeSpan.FromMilliseconds(25),
            ConnectTimeout = TimeSpan.FromSeconds(3),
        };
        var server = UdpServerTransport.Bind("soak-server", new IPEndPoint(IPAddress.Loopback, 0), options);
        var rig = new UdpSoakRig(server);
        for (var i = 0; i < peerCount; i++)
        {
            rig._clients.Add(new UdpClientTransport($"soak-client-{i}", server.BoundEndpoint, options));
        }

        return rig;
    }

    public INetTransport Server => _server;

    public int PeerCount => _clients.Count;

    public INetTransport Client(int peerIndex) => _clients[peerIndex];

    public ConnectionId ServerSentinel => UdpClientTransport.ServerSentinelId;

    public void Dispose()
    {
        foreach (var client in _clients)
        {
            client.Dispose();
        }

        _clients.Clear();
        _server.Dispose();
    }
}
