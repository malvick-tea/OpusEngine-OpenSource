using System;

namespace Opus.Net.Udp.Transport;

/// <summary>
/// Tuneable knobs for both <see cref="UdpClientTransport"/> and
/// <see cref="UdpServerTransport"/>. The defaults are sized for a Phase-32 local test
/// match (60 Hz sim, snapshots every tick): heartbeat once a second is generous, and a
/// ten-second dead-peer cut-off leaves room for client-side network hiccups.
/// </summary>
/// <remarks>
/// Smaller values are appropriate for unit tests (so a hang is loud and a disconnect
/// closes in tens of milliseconds). Runtime never wants sub-second heartbeats — that
/// just adds noise to the line — but values down to <c>50 ms</c> are stable on loopback.
/// </remarks>
public sealed record UdpTransportOptions
{
    /// <summary>How often the transport emits a <see cref="Frame.UdpFrameKind.Heartbeat"/>
    /// frame when no other outbound traffic has flowed within the same window. Must be
    /// strictly less than <see cref="DeadlineDuration"/> — otherwise a quiet line
    /// disconnects before the first heartbeat is even sent.</summary>
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Maximum time without any inbound frame before the peer is considered
    /// dead. On expiry the transport queues a <c>NetEvent.Disconnected</c> and tears
    /// the slot down — same shape as a clean Disconnect.</summary>
    public TimeSpan DeadlineDuration { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>How long the receive worker blocks on <c>Socket.ReceiveFrom</c> before
    /// looping into the housekeeping pass. Smaller values give snappier heartbeat /
    /// timeout reaction at the cost of more wakeups; <c>250 ms</c> hits a comfortable
    /// balance for runtime.</summary>
    public TimeSpan ReceivePollInterval { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>Total budget the client gives the server to answer <c>Hello</c> with
    /// <c>WelcomeAck</c> before the transport gives up and surfaces a Disconnected event.
    /// Server-side this field has no effect.</summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Maximum number of concurrent peer slots the <see cref="UdpServerTransport"/>
    /// will hold. A fresh <see cref="Frame.UdpFrameKind.Hello"/> from an unknown endpoint
    /// is rejected (no slot allocated, no <c>Connected</c> event, no reply) once the table is
    /// full, so an unauthenticated party cannot exhaust server memory by flooding Hellos from
    /// many source endpoints faster than the dead-peer timeout reaps them. Existing peers and
    /// repeat Hellos from a known endpoint are never affected by the cap. Must be at least 1;
    /// <see cref="UdpClientTransport"/> ignores this field. The default (64) sits well above
    /// the Opus 0.1 alpha's 20-player target with headroom for reconnect churn (a peer
    /// reconnecting from a new ephemeral port briefly holds two slots until the old one times
    /// out).</summary>
    public int MaxConcurrentPeers { get; init; } = DefaultMaxConcurrentPeers;

    /// <summary>Default concurrent-peer cap. Sized for the 20-player alpha plus reconnect
    /// headroom; raise it via options for a larger server deployment.</summary>
    public const int DefaultMaxConcurrentPeers = 64;

    /// <summary>Maximum number of inbound events the transport holds in its poll queue before
    /// shedding further inbound payloads. A connected peer that sends payload frames faster than
    /// the consumer drains <c>Poll</c> — or while the game thread stalls — cannot grow the queue
    /// without bound: once it is full, additional payload events are dropped (UDP is lossy, so a
    /// shed payload is in-protocol) and counted via <c>DroppedInboundPayloadCount</c>, while
    /// connection-state events (Connected / Disconnected) are never dropped so peer bookkeeping
    /// stays correct. Must be at least 1. The default (1024) sits far above the steady-state queue
    /// depth of a 60 Hz match drained every tick while capping worst-case queued memory.</summary>
    public int MaxInboundQueuedEvents { get; init; } = DefaultMaxInboundQueuedEvents;

    /// <summary>Default inbound poll-queue cap. Generous for the alpha's per-tick drain cadence;
    /// raise it for a deployment that polls less frequently or carries larger bursts.</summary>
    public const int DefaultMaxInboundQueuedEvents = 1024;

    /// <summary>Burst capacity of the per-peer inbound payload rate limiter, in payloads. The
    /// <see cref="MaxInboundQueuedEvents"/> cap bounds <em>total</em> queued memory, but on its own
    /// it lets a single connected peer fill the whole shared queue and starve every other peer; the
    /// per-peer token bucket closes that fairness gap by bounding how fast one peer may enqueue,
    /// independent of the global queue depth. This value is the most back-to-back payloads a peer
    /// may enqueue after an idle stretch (the bucket starts full and refills at
    /// <see cref="InboundPayloadRefillPerSecondPerPeer"/>). Must be at least 1; the
    /// <see cref="UdpServerTransport"/> enforces it, the <see cref="UdpClientTransport"/> ignores it
    /// (a client has one trusted server peer, already bounded by <see cref="MaxInboundQueuedEvents"/>).
    /// The default (256) absorbs several seconds of a 60 Hz client plus reconnect / snapshot bursts
    /// without ever throttling legitimate alpha traffic.</summary>
    public int MaxInboundPayloadBurstPerPeer { get; init; } = DefaultMaxInboundPayloadBurstPerPeer;

    /// <summary>Default per-peer inbound burst capacity. Sized so the rate limiter never bites
    /// legitimate alpha traffic; only a flood far above a 60 Hz cadence is shed.</summary>
    public const int DefaultMaxInboundPayloadBurstPerPeer = 256;

    /// <summary>Sustained refill rate of the per-peer inbound payload rate limiter, in payloads per
    /// second — the long-run ceiling on how fast one peer may enqueue once its
    /// <see cref="MaxInboundPayloadBurstPerPeer"/> burst is spent. A peer flooding above this rate is
    /// clamped to it (surplus payloads are shed and counted via
    /// <see cref="UdpServerTransport.RateLimitedInboundPayloadCount"/>); UDP is lossy, so a shed
    /// payload is in-protocol. Must be at least 1; ignored by <see cref="UdpClientTransport"/>. The
    /// default (512/s) sits well above a 60 Hz client emitting a payload per tick, leaving generous
    /// headroom for chatty reliable-layer acks and resends.</summary>
    public int InboundPayloadRefillPerSecondPerPeer { get; init; } = DefaultInboundPayloadRefillPerSecondPerPeer;

    /// <summary>Default per-peer inbound sustained refill rate. Comfortably above a 60 Hz client's
    /// steady-state payload rate; raise it for a consumer with a chattier inbound protocol.</summary>
    public const int DefaultInboundPayloadRefillPerSecondPerPeer = 512;

    /// <summary>Validates every bounded option against its shared invariant (each cap must be at
    /// least 1). Called by <see cref="UdpServerTransport.Bind"/> and the
    /// <see cref="UdpClientTransport"/> constructor so a misconfigured cap fails fast at construction
    /// with a precise <see cref="ArgumentOutOfRangeException"/> naming the offending option, instead
    /// of surfacing later as a confusing runtime symptom. The client ignores the server-only caps at
    /// runtime but still validates them so one options record cannot be silently valid for one
    /// transport and invalid for the other.</summary>
    public void Validate()
    {
        ThrowIfLessThanOne(MaxConcurrentPeers, nameof(MaxConcurrentPeers));
        ThrowIfLessThanOne(MaxInboundQueuedEvents, nameof(MaxInboundQueuedEvents));
        ThrowIfLessThanOne(MaxInboundPayloadBurstPerPeer, nameof(MaxInboundPayloadBurstPerPeer));
        ThrowIfLessThanOne(InboundPayloadRefillPerSecondPerPeer, nameof(InboundPayloadRefillPerSecondPerPeer));
    }

    private static void ThrowIfLessThanOne(int value, string optionName)
    {
        if (value < 1)
        {
            throw new ArgumentOutOfRangeException(
                optionName,
                value,
                $"UdpTransportOptions.{optionName} must be at least 1.");
        }
    }

    /// <summary>Default options — runtime-tuned (1s heartbeat, 10s deadline, 5s connect
    /// budget, 250ms poll wakeup, 64-peer cap, 256-burst / 512-per-second per-peer inbound rate).
    /// Tests should construct a smaller variant locally.</summary>
    public static UdpTransportOptions Default { get; } = new();
}
