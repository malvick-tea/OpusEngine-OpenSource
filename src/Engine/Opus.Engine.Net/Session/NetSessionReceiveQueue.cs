using System;
using System.Collections.Generic;
using Opus.Net.Transport;

namespace Opus.Engine.Net.Session;

/// <summary>
/// Bounded FIFO queue of received payloads for a <see cref="NetSession"/>. Drops the
/// oldest entry when the bound is reached so callers always observe the most recent
/// activity rather than getting stuck on stale data. Extracted from
/// <see cref="NetSession"/> so the orchestrator stays under the class-size budget and so
/// the dropping policy is testable on its own.
/// </summary>
internal sealed class NetSessionReceiveQueue
{
    private readonly Queue<QueuedPayload> _queue = new();
    private readonly int _maxQueued;

    public NetSessionReceiveQueue(int maxQueuedPayloads)
    {
        if (maxQueuedPayloads < NetSessionOptions.MinimumMaxQueuedPayloads)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxQueuedPayloads),
                $"NetSessionReceiveQueue capacity must be at least {NetSessionOptions.MinimumMaxQueuedPayloads}.");
        }

        _maxQueued = maxQueuedPayloads;
    }

    public int Count => _queue.Count;

    /// <summary>Adds a payload to the tail. Returns true when an older entry had to be
    /// dropped to make room; the caller is expected to record this as a dropped-queue
    /// counter.</summary>
    public bool Enqueue(ConnectionId from, byte[] payload)
    {
        var dropped = false;
        if (_queue.Count >= _maxQueued)
        {
            _queue.Dequeue();
            dropped = true;
        }

        _queue.Enqueue(new QueuedPayload(from, payload));
        return dropped;
    }

    /// <summary>Pops the head entry, or returns false when the queue is empty.</summary>
    public bool TryDequeue(out ConnectionId from, out byte[] payload)
    {
        if (_queue.Count == 0)
        {
            from = ConnectionId.None;
            payload = Array.Empty<byte>();
            return false;
        }

        var entry = _queue.Dequeue();
        from = entry.From;
        payload = entry.Payload;
        return true;
    }

    /// <summary>Removes every entry without dispatching them.</summary>
    public void Clear() => _queue.Clear();

    private readonly record struct QueuedPayload(ConnectionId From, byte[] Payload);
}
