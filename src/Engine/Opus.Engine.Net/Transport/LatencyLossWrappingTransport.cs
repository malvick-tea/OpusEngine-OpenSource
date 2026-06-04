using System;
using System.Collections.Generic;
using Opus.Net.Transport;

namespace Opus.Engine.Net.Transport;

/// <summary>
/// Engine-side wrapping transport that injects deterministic packet loss and added
/// latency into outbound traffic. The wrapper holds an inner <see cref="INetTransport"/>
/// and forwards every contract member through, except that <see cref="Send"/> may either
/// drop the datagram (loss injection) or defer it until <see cref="Poll"/> flushes a
/// deadline-based send queue (latency injection).
/// <para>
/// The wrapper is engine-neutral: it imposes no protocol shape, knows nothing about
/// match rules or session lifecycle, and never inspects payload bytes. It exists so the
/// M11 stress harness can reproduce degraded-network conditions over the same loopback
/// or UDP rigs M8 already validates.
/// </para>
/// <para>
/// Threading: single-affinity. The stress harness drives the wrapper from one thread;
/// the wrapper does not protect its scheduled-send queue with locks because the
/// underlying soak harness is itself single-threaded.
/// </para>
/// </summary>
public sealed class LatencyLossWrappingTransport : INetTransport
{
    private readonly INetTransport _inner;
    private readonly LatencyLossInjectionProfile _profile;
    private readonly TimeProvider _time;
    private readonly Random _random;
    private readonly Random _inboundRandom;
    private readonly Queue<ScheduledSend> _scheduled = new();
    private readonly Queue<ScheduledReceive> _scheduledInbound = new();
    private readonly List<NetEvent> _pollScratch = new();
    private readonly bool _ownsInner;
    private long _droppedPacketCount;
    private long _delayedPacketCount;
    private long _inboundAttemptCount;
    private long _inboundDroppedPacketCount;
    private long _inboundDelayedPacketCount;
    private bool _disposed;

    /// <summary>Creates a wrapping transport over <paramref name="inner"/>. The
    /// <paramref name="ownsInner"/> flag controls whether <see cref="Dispose"/> disposes
    /// the inner transport.</summary>
    public LatencyLossWrappingTransport(
        INetTransport inner,
        LatencyLossInjectionProfile profile,
        TimeProvider? time = null,
        bool ownsInner = false)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();
        _inner = inner;
        _profile = profile;
        _time = time ?? TimeProvider.System;
        _random = new Random(profile.Seed);
        _inboundRandom = new Random(profile.InboundSeed);
        _ownsInner = ownsInner;
    }

    /// <summary>Number of outbound datagrams dropped by loss injection.</summary>
    public long DroppedPacketCount => _droppedPacketCount;

    /// <summary>Number of outbound datagrams that were delayed (sent after the
    /// configured added latency, not immediately).</summary>
    public long DelayedPacketCount => _delayedPacketCount;

    /// <summary>Number of scheduled sends still waiting for their deadline.</summary>
    public int PendingScheduledSendCount => _scheduled.Count;

    /// <summary>Number of inbound <c>Received</c> events observed by <see cref="Poll"/>
    /// across the wrapper's lifetime. The denominator for the inbound drop fraction;
    /// control-plane events do not contribute.</summary>
    public long InboundAttemptCount => _inboundAttemptCount;

    /// <summary>Number of inbound <c>Received</c> events the wrapper dropped before
    /// surfacing them to the caller.</summary>
    public long InboundDroppedPacketCount => _inboundDroppedPacketCount;

    /// <summary>Number of inbound <c>Received</c> events the wrapper queued behind the
    /// configured inbound latency deadline.</summary>
    public long InboundDelayedPacketCount => _inboundDelayedPacketCount;

    /// <summary>Number of inbound <c>Received</c> events still waiting for their
    /// inbound-latency deadline.</summary>
    public int PendingScheduledReceiveCount => _scheduledInbound.Count;

    /// <inheritdoc />
    public string Name => _inner.Name;

    /// <inheritdoc />
    public bool IsOpen => !_disposed && _inner.IsOpen;

    /// <inheritdoc />
    public bool Send(ConnectionId target, ReadOnlySpan<byte> payload)
    {
        ThrowIfDisposed();
        if (_profile.LossRate > 0.0 && _random.NextDouble() < _profile.LossRate)
        {
            _droppedPacketCount++;
            return true;
        }

        if (_profile.AddedLatency <= TimeSpan.Zero)
        {
            return _inner.Send(target, payload);
        }

        _scheduled.Enqueue(new ScheduledSend(
            Target: target,
            Payload: payload.ToArray(),
            ReadyAtUtc: _time.GetUtcNow() + _profile.AddedLatency));
        _delayedPacketCount++;
        return true;
    }

    /// <inheritdoc />
    public void Poll(List<NetEvent> into)
    {
        ArgumentNullException.ThrowIfNull(into);
        ThrowIfDisposed();
        FlushReadyScheduledSends();
        into.Clear();
        FlushReadyScheduledReceives(into);
        _pollScratch.Clear();
        _inner.Poll(_pollScratch);
        FilterInnerEvents(_pollScratch, into);
    }

    /// <inheritdoc />
    public void Disconnect(ConnectionId target)
    {
        if (_disposed)
        {
            return;
        }

        _inner.Disconnect(target);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _scheduled.Clear();
        _scheduledInbound.Clear();
        _pollScratch.Clear();
        if (_ownsInner)
        {
            _inner.Dispose();
        }
    }

    private void FlushReadyScheduledSends()
    {
        if (_scheduled.Count == 0)
        {
            return;
        }

        var now = _time.GetUtcNow();
        while (_scheduled.Count > 0 && _scheduled.Peek().ReadyAtUtc <= now)
        {
            var scheduled = _scheduled.Dequeue();
            _inner.Send(scheduled.Target, scheduled.Payload);
        }
    }

    private void FlushReadyScheduledReceives(List<NetEvent> into)
    {
        if (_scheduledInbound.Count == 0)
        {
            return;
        }

        var now = _time.GetUtcNow();
        while (_scheduledInbound.Count > 0 && _scheduledInbound.Peek().ReadyAtUtc <= now)
        {
            into.Add(_scheduledInbound.Dequeue().Event);
        }
    }

    private void FilterInnerEvents(List<NetEvent> source, List<NetEvent> destination)
    {
        foreach (var ev in source)
        {
            if (ev.Kind != NetEventKind.Received)
            {
                destination.Add(ev);
                continue;
            }

            _inboundAttemptCount++;
            if (_profile.InboundLossRate > 0.0 && _inboundRandom.NextDouble() < _profile.InboundLossRate)
            {
                _inboundDroppedPacketCount++;
                continue;
            }

            if (_profile.InboundAddedLatency <= TimeSpan.Zero)
            {
                destination.Add(ev);
                continue;
            }

            _scheduledInbound.Enqueue(new ScheduledReceive(
                Event: ev,
                ReadyAtUtc: _time.GetUtcNow() + _profile.InboundAddedLatency));
            _inboundDelayedPacketCount++;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(LatencyLossWrappingTransport));
        }
    }

    private readonly record struct ScheduledSend(
        ConnectionId Target,
        byte[] Payload,
        DateTimeOffset ReadyAtUtc);

    private readonly record struct ScheduledReceive(
        NetEvent Event,
        DateTimeOffset ReadyAtUtc);
}
