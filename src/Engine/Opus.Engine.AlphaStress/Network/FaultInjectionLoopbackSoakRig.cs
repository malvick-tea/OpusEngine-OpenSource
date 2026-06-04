using System;
using System.Collections.Generic;
using Opus.Engine.Net.Soak;
using Opus.Engine.Net.Transport;
using Opus.Net.Loopback;
using Opus.Net.Transport;

namespace Opus.Engine.AlphaStress.Network;

/// <summary>
/// Loopback-backed <see cref="INetSoakRig"/> that wraps every client transport with a
/// <see cref="LatencyLossWrappingTransport"/> so the M11 stress harness can reproduce
/// degraded-network conditions over the same loopback shape M8 already validates.
/// Owns the underlying hub and every wrapping transport; <see cref="Dispose"/> releases
/// each in topological order.
/// </summary>
/// <remarks>
/// Per-client wrapper seeds are derived as <c>injection.Seed + peerIndex</c> so two
/// stress runs with the same <see cref="LatencyLossInjectionProfile"/> reproduce
/// identical per-peer drop patterns while still differing across peers (the harness
/// validates this in
/// <c>FaultInjectionLoopbackSoakRigTests.Create_per_peer_wrappers_use_offset_seeds</c>).
/// Only the client side is wrapped — the server hub stays unmodified so server echoes
/// flow back cleanly, mirroring the "upload loss" shape a real alpha tester pool sees.
/// </remarks>
public sealed class FaultInjectionLoopbackSoakRig : INetSoakRig
{
    private readonly LoopbackTransportHub _hub;
    private readonly List<LatencyLossWrappingTransport> _clientWrappers;
    private bool _disposed;

    private FaultInjectionLoopbackSoakRig(
        LoopbackTransportHub hub,
        List<LatencyLossWrappingTransport> clientWrappers)
    {
        _hub = hub;
        _clientWrappers = clientWrappers;
    }

    /// <summary>Builds a rig with <paramref name="peerCount"/> wrapped client transports
    /// already accepted by the hub. The hub and every wrapper are owned by the rig and
    /// released on <see cref="Dispose"/>.</summary>
    public static FaultInjectionLoopbackSoakRig Create(
        int peerCount,
        LatencyLossInjectionProfile injection,
        TimeProvider? time = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(peerCount);
        ArgumentNullException.ThrowIfNull(injection);
        injection.Validate();

        var hub = LoopbackTransportHub.Create();
        var wrappers = new List<LatencyLossWrappingTransport>(peerCount);
        try
        {
            for (var peerIndex = 0; peerIndex < peerCount; peerIndex++)
            {
                var client = hub.Accept($"alpha-stress-client-{peerIndex}").Client;
                var perPeerProfile = injection with
                {
                    Seed = injection.Seed + peerIndex,
                    InboundSeed = injection.InboundSeed + peerIndex,
                };
                var wrapper = new LatencyLossWrappingTransport(
                    inner: client,
                    profile: perPeerProfile,
                    time: time,
                    ownsInner: true);
                wrappers.Add(wrapper);
            }
        }
        catch
        {
            DisposeAll(hub, wrappers);
            throw;
        }

        return new FaultInjectionLoopbackSoakRig(hub, wrappers);
    }

    /// <summary>Server-side transport. Stays unwrapped so server echoes flow cleanly.</summary>
    public INetTransport Server => _hub;

    /// <inheritdoc />
    public int PeerCount => _clientWrappers.Count;

    /// <inheritdoc />
    public INetTransport Client(int peerIndex) => _clientWrappers[peerIndex];

    /// <inheritdoc />
    public ConnectionId ServerSentinel => LoopbackTransportHub.HubSentinelId;

    /// <summary>Total outbound packets every client wrapper dropped during the workload.</summary>
    public long TotalDroppedPackets
    {
        get
        {
            long total = 0;
            foreach (var wrapper in _clientWrappers)
            {
                total += wrapper.DroppedPacketCount;
            }

            return total;
        }
    }

    /// <summary>Total outbound packets every client wrapper queued behind the configured
    /// added-latency deadline during the workload.</summary>
    public long TotalDelayedPackets
    {
        get
        {
            long total = 0;
            foreach (var wrapper in _clientWrappers)
            {
                total += wrapper.DelayedPacketCount;
            }

            return total;
        }
    }

    /// <summary>Total inbound <c>Received</c> events every client wrapper observed
    /// before applying the inbound fault-injection filter.</summary>
    public long TotalInboundAttempts
    {
        get
        {
            long total = 0;
            foreach (var wrapper in _clientWrappers)
            {
                total += wrapper.InboundAttemptCount;
            }

            return total;
        }
    }

    /// <summary>Total inbound events every client wrapper dropped before surfacing.</summary>
    public long TotalInboundDroppedPackets
    {
        get
        {
            long total = 0;
            foreach (var wrapper in _clientWrappers)
            {
                total += wrapper.InboundDroppedPacketCount;
            }

            return total;
        }
    }

    /// <summary>Total inbound events every client wrapper queued behind the configured
    /// inbound-latency deadline.</summary>
    public long TotalInboundDelayedPackets
    {
        get
        {
            long total = 0;
            foreach (var wrapper in _clientWrappers)
            {
                total += wrapper.InboundDelayedPacketCount;
            }

            return total;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DisposeAll(_hub, _clientWrappers);
        _clientWrappers.Clear();
    }

    private static void DisposeAll(LoopbackTransportHub hub, List<LatencyLossWrappingTransport> wrappers)
    {
        foreach (var wrapper in wrappers)
        {
            try
            {
                wrapper.Dispose();
            }
            catch
            {
                // Swallow per-wrapper teardown failures so the remaining wrappers and the
                // hub still release; a stress rig must never leak resources on construction
                // failure or repeated dispose.
            }
        }

        hub.Dispose();
    }
}
