namespace Opus.Engine.Net.Session;

/// <summary>
/// Discriminator for the events a <see cref="NetSession"/> surfaces through its tick
/// callback. Mirrors transport-level <see cref="Opus.Net.Transport.NetEventKind"/> for
/// peer membership while adding session-lifecycle and reconnect markers a tester or game
/// host needs to log.
/// </summary>
public enum NetSessionEventKind
{
    /// <summary>Session opened its first transport. Carries no peer id.</summary>
    Started,

    /// <summary>A peer transitioned to <c>Connected</c> on the underlying transport.</summary>
    PeerConnected,

    /// <summary>A peer transitioned to <c>Disconnected</c> on the underlying transport.</summary>
    PeerDisconnected,

    /// <summary>A payload arrived from a known peer. The byte length is on the event;
    /// the bytes themselves are delivered via <see cref="INetSession.NextReceivedPayload"/>
    /// so the session keeps payload ownership decisions on one path.</summary>
    PayloadReceived,

    /// <summary>Session scheduled a reconnect attempt and is now in
    /// <see cref="NetSessionState.Reconnecting"/>. Client role only.</summary>
    ReconnectScheduled,

    /// <summary>Session attempted to rebuild the client transport via the supplied
    /// factory. Whether the new transport will connect is surfaced later through
    /// <see cref="PeerConnected"/> or another <see cref="ReconnectScheduled"/>.</summary>
    ReconnectAttempted,

    /// <summary>Session ran out of reconnect attempts and transitioned to
    /// <see cref="NetSessionState.Faulted"/>.</summary>
    ReconnectExhausted,

    /// <summary>Session caught an exception while polling or interacting with the
    /// transport factory and transitioned to <see cref="NetSessionState.Faulted"/>.</summary>
    TransportFault,

    /// <summary>Session left every non-Idle state through a clean
    /// <see cref="INetSession.RequestStop"/> call.</summary>
    Stopped,
}
