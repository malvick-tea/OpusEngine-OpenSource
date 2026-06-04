namespace Opus.Engine.Net.Session;

/// <summary>
/// Engine-visible state machine for <see cref="INetSession"/>. The state is observable
/// every <see cref="INetSession.Tick"/> through <see cref="INetSession.State"/>; transitions
/// are surfaced as <see cref="NetSessionEvent"/> records.
/// </summary>
public enum NetSessionState
{
    /// <summary>Session has been created but not started, or has stopped after a clean
    /// <see cref="INetSession.RequestStop"/>. Polling does nothing.</summary>
    Idle = 0,

    /// <summary>Session has a transport and is waiting for its first <c>Connected</c>
    /// event. Client sessions enter this on start and after every reconnect attempt;
    /// server sessions enter this on start and stay here until at least one peer arrives.</summary>
    Connecting = 1,

    /// <summary>Session has at least one connected peer; <see cref="INetSession.Send"/>
    /// is the only state where the call may succeed.</summary>
    Connected = 2,

    /// <summary>Session lost its last peer but a reconnect policy is in force; the
    /// session is waiting for the configured backoff window before retrying. Client role
    /// only — server sessions transition straight back to <see cref="Connecting"/>.</summary>
    Reconnecting = 3,

    /// <summary>Session has terminated abnormally. Reasons include reconnect budget
    /// exhaustion, transport exception, or a fatal fault recorded against the latest
    /// snapshot. <see cref="INetSession.Send"/> always returns <c>false</c>.</summary>
    Faulted = 4,

    /// <summary>Session has been disposed. Every public surface other than
    /// <see cref="INetSession.State"/> and <see cref="INetSession.Statistics"/> becomes
    /// a no-op.</summary>
    Disposed = 5,
}
