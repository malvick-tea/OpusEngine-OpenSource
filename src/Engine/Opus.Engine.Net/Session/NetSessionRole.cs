namespace Opus.Engine.Net.Session;

/// <summary>
/// Whether a <see cref="NetSession"/> is driving the client side or the server side of a
/// connection. The role determines reconnect behavior: client sessions schedule a
/// reconnect after losing the last peer (via the supplied factory); server sessions stay
/// bound and treat peer churn as ordinary <see cref="NetSessionEventKind.PeerDisconnected"/>
/// activity.
/// </summary>
public enum NetSessionRole
{
    /// <summary>Single-peer outbound role. Reconnect policy applies. The transport is
    /// recycled by the configured factory after a Disconnected event drains.</summary>
    Client = 0,

    /// <summary>Multi-peer inbound role. No reconnect logic — the bound socket stays open
    /// and the session simply tracks accepted peers. Reconnect options are ignored.</summary>
    Server = 1,
}
