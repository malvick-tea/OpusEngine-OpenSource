namespace Opus.Engine.Net;

/// <summary>
/// Stable diagnostic codes emitted by the engine-level network runtime. Codes are
/// append-only: never renumber or repurpose an entry once it has shipped. The
/// <c>OPDX-NET-*</c> namespace is reserved for session lifecycle, reconnect bookkeeping,
/// transport fault classification, and soak harness findings. Mirrors the contract
/// established for overlay (<c>OPDX-OVR-*</c>), reports (<c>OPDX-REP-*</c>), and rolling
/// log (<c>OPDX-LOG-*</c>) namespaces in
/// <see cref="Opus.Engine.Diagnostics.DiagnosticCodes"/>.
/// </summary>
public static class NetDiagnosticCodes
{
    /// <summary>Session opened a transport instance and entered <c>Connecting</c>.</summary>
    public const string SessionStarted = "OPDX-NET-001";

    /// <summary>Session observed a remote peer transitioning to <c>Connected</c>.</summary>
    public const string SessionPeerConnected = "OPDX-NET-002";

    /// <summary>Session observed a remote peer transitioning to <c>Disconnected</c>.</summary>
    public const string SessionPeerDisconnected = "OPDX-NET-003";

    /// <summary>Session scheduled a reconnect attempt after losing every peer (client role).</summary>
    public const string SessionReconnectScheduled = "OPDX-NET-004";

    /// <summary>Session executed a reconnect attempt and rebound the transport.</summary>
    public const string SessionReconnectAttempted = "OPDX-NET-005";

    /// <summary>Session exhausted its reconnect budget; entered <c>Faulted</c>.</summary>
    public const string SessionReconnectExhausted = "OPDX-NET-006";

    /// <summary>Session caught an exception while polling or sending; recorded as a fault.</summary>
    public const string SessionTransportFault = "OPDX-NET-007";

    /// <summary>Session stopped cleanly through <see cref="Session.INetSession.RequestStop"/>.</summary>
    public const string SessionStopped = "OPDX-NET-008";

    /// <summary>Soak harness observed a peer that failed to reach the <c>Connected</c> state.</summary>
    public const string SoakPeerUnconnected = "OPDX-NET-101";

    /// <summary>Soak harness observed a received payload that did not match its expected sequence/CRC.</summary>
    public const string SoakPayloadCorruption = "OPDX-NET-102";

    /// <summary>Soak harness observed a packet that never arrived inside its delivery budget.</summary>
    public const string SoakPacketDropped = "OPDX-NET-103";

    /// <summary>Soak harness wall-clock budget elapsed before the workload completed.</summary>
    public const string SoakBudgetExceeded = "OPDX-NET-104";

    /// <summary>Soak harness peer observed a transport-level fault during the run.</summary>
    public const string SoakTransportFault = "OPDX-NET-105";
}
