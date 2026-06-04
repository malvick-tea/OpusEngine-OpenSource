using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Opus.Net.Transport;
using Opus.Net.Udp.Transport;

namespace Opus.Net.Udp.Tests.Transport;

/// <summary>
/// Test helper that aggregates poll output from a <see cref="UdpServerTransport"/> + one
/// or more <see cref="UdpClientTransport"/> instances into per-side event lists. Tests
/// build expectations against these lists with <see cref="WaitFor"/> instead of arbitrary
/// sleeps.
/// </summary>
internal sealed class UdpIntegrationHarness : IDisposable
{
    private readonly List<NetEvent> _scratch = new();

    private UdpIntegrationHarness(
        UdpServerTransport server,
        Func<UdpTransportOptions> optionsFactory)
    {
        Server = server;
        ServerEvents = new List<NetEvent>();
        ClientEntries = new List<ClientEntry>();
        OptionsFactory = optionsFactory;
    }

    public UdpServerTransport Server { get; }

    public List<NetEvent> ServerEvents { get; }

    public List<ClientEntry> ClientEntries { get; }

    public Func<UdpTransportOptions> OptionsFactory { get; }

    /// <summary>Test-tuned timing: snappy heartbeats so the dead-peer path fires inside a
    /// second, but not so tight that GC / scheduler hiccups under <c>dotnet test</c>
    /// flake. Runtime options sit ~10× looser (<see cref="UdpTransportOptions.Default"/>).</summary>
    public static UdpTransportOptions FastOptions() => new()
    {
        HeartbeatInterval = TimeSpan.FromMilliseconds(100),
        DeadlineDuration = TimeSpan.FromMilliseconds(800),
        ReceivePollInterval = TimeSpan.FromMilliseconds(50),
        ConnectTimeout = TimeSpan.FromSeconds(2),
    };

    public static UdpIntegrationHarness Start(string serverName = "server")
    {
        return Start(serverName, FastOptions);
    }

    public static UdpIntegrationHarness Start(string serverName, Func<UdpTransportOptions> optionsFactory)
    {
        var listenEndpoint = new IPEndPoint(IPAddress.Loopback, 0);
        var server = UdpServerTransport.Bind(serverName, listenEndpoint, optionsFactory());
        return new UdpIntegrationHarness(server, optionsFactory);
    }

    public ClientEntry AddClient(string name = "client")
    {
        Drain();
        var connectedCountBefore = ServerEvents.Count(e => e.Kind == NetEventKind.Connected);
        var client = new UdpClientTransport(name, Server.BoundEndpoint, OptionsFactory());
        var entry = new ClientEntry(client) { ServerSideIndexAtAdd = connectedCountBefore };
        ClientEntries.Add(entry);
        return entry;
    }

    public void Drain()
    {
        Server.Poll(_scratch);
        ServerEvents.AddRange(_scratch);
        foreach (var entry in ClientEntries)
        {
            entry.Client.Poll(_scratch);
            entry.Events.AddRange(_scratch);
        }
    }

    public bool WaitFor(Func<bool> predicate, TimeSpan? timeout = null)
    {
        var deadline = Environment.TickCount64 + (long)(timeout ?? TimeSpan.FromSeconds(3)).TotalMilliseconds;
        while (Environment.TickCount64 < deadline)
        {
            Drain();
            if (predicate())
            {
                return true;
            }

            Thread.Sleep(20);
        }

        Drain();
        return predicate();
    }

    public ConnectionId WaitForConnected(ClientEntry entry, TimeSpan? timeout = null)
    {
        if (!WaitFor(() => entry.Events.Any(e => e.Kind == NetEventKind.Connected), timeout))
        {
            throw new TimeoutException($"client {entry.Client.Name} did not observe Connected");
        }

        return entry.Events.First(e => e.Kind == NetEventKind.Connected).Connection;
    }

    public ConnectionId WaitForServerToAccept(ClientEntry entry, TimeSpan? timeout = null)
    {
        if (!WaitFor(
                () => ServerEvents.Count(e => e.Kind == NetEventKind.Connected) > entry.ServerSideIndexAtAdd,
                timeout))
        {
            throw new TimeoutException("server did not accept the requested client");
        }

        return ServerEvents
            .Where(e => e.Kind == NetEventKind.Connected)
            .Skip(entry.ServerSideIndexAtAdd)
            .First()
            .Connection;
    }

    public void Dispose()
    {
        foreach (var entry in ClientEntries)
        {
            entry.Client.Dispose();
        }

        Server.Dispose();
    }

    internal sealed class ClientEntry
    {
        public ClientEntry(UdpClientTransport client)
        {
            Client = client;
            Events = new List<NetEvent>();
            ServerSideIndexAtAdd = 0;
        }

        public UdpClientTransport Client { get; }

        public List<NetEvent> Events { get; }

        public int ServerSideIndexAtAdd { get; set; }
    }
}
