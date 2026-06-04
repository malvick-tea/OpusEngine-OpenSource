namespace Opus.Net.Transport;

/// <summary>
/// Opaque, stable handle for a single peer connection. Assigned by the transport when a
/// connection is accepted and stays the same until that peer disconnects. Comparing two
/// <see cref="ConnectionId"/>s with <c>==</c> is safe; sorting them is not meaningful
/// — record-struct equality is the only relation defined.
/// </summary>
/// <remarks>
/// <para>
/// The wrapped <see cref="ulong"/> is implementation-private: the loopback transport
/// uses a sequential counter, a future UDP transport may pack (endpoint hash | salt) so
/// reconnects don't reuse a stale id. Callers must treat the value as opaque.
/// </para>
/// <para>
/// <c>Zero</c> is reserved as the sentinel "no connection" and never assigned to a real
/// peer.
/// </para>
/// </remarks>
public readonly record struct ConnectionId(ulong Value)
{
    /// <summary>Sentinel — never assigned to a real connection.</summary>
    public static readonly ConnectionId None = new(0UL);

    /// <summary>True when this is a live connection identifier (not the
    /// <see cref="None"/> sentinel).</summary>
    public bool IsValid => Value != 0UL;

    public override string ToString() => IsValid ? $"conn#{Value}" : "conn#none";
}
