namespace Opus.Net.Transport;

/// <summary>Discriminator for <see cref="NetEvent"/> — one of the three transport-level
/// notifications a host pumps off its receive queue.</summary>
public enum NetEventKind
{
    /// <summary>A peer's connection became live. <see cref="NetEvent.Connection"/> is
    /// valid; <see cref="NetEvent.Payload"/> is empty.</summary>
    Connected,

    /// <summary>A previously-connected peer dropped (clean close or timeout — the
    /// transport does not distinguish). <see cref="NetEvent.Connection"/> is valid;
    /// payload is empty. After this event the id must not be reused.</summary>
    Disconnected,

    /// <summary>A datagram arrived. <see cref="NetEvent.Connection"/> identifies the
    /// sender; <see cref="NetEvent.Payload"/> holds the bytes (owned by the consumer,
    /// safe to mutate or hold past the dispatch).</summary>
    Received,
}
