namespace Opus.Engine.Net.Session;

/// <summary>
/// Stable classification of why a <see cref="NetSession"/> transitioned to
/// <see cref="NetSessionState.Faulted"/>. Append-only; never renumber once shipped.
/// </summary>
public enum NetSessionFaultCode
{
    /// <summary>Reconnect policy exhausted its configured attempt budget.</summary>
    ReconnectBudgetExhausted = 1,

    /// <summary>Reconnect transport factory threw while creating a fresh transport.</summary>
    ReconnectFactoryThrew = 2,

    /// <summary>Transport poll, send, or disconnect threw an exception during a tick.</summary>
    TransportException = 3,

    /// <summary>Transport returned an invalid state during a tick (closed handles, etc.).</summary>
    TransportInvalidState = 4,
}
