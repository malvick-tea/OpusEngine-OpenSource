using System;

namespace Opus.Engine.Net.Session;

/// <summary>
/// Construction-time configuration for <see cref="NetSession"/>. Carries the role, the
/// stable display name, the reconnect policy (ignored for server role), and the maximum
/// number of payload datagrams the session retains for <see cref="INetSession.NextReceivedPayload"/>
/// callers before older entries are dropped to keep memory bounded.
/// </summary>
/// <param name="Role">Whether this session drives the client or server side.</param>
/// <param name="DisplayName">
/// Stable display name surfaced in logs and telemetry rows. The name is preserved
/// across reconnects so a tester's log filter keeps working through a transport bounce.
/// </param>
/// <param name="Reconnect">Reconnect cadence (client role); ignored when
/// <see cref="Role"/> is <see cref="NetSessionRole.Server"/>.</param>
/// <param name="MaxQueuedPayloads">
/// Maximum number of received payloads the session retains in its receive queue between
/// ticks. Older entries are dropped on overflow and a fault diagnostic is emitted so the
/// game-side consumer knows to drain on a more frequent cadence.
/// </param>
public sealed record NetSessionOptions(
    NetSessionRole Role,
    string DisplayName,
    NetReconnectPolicy? Reconnect = null,
    int MaxQueuedPayloads = 2048)
{
    /// <summary>Default ceiling for the receive queue. Sized for a 60 Hz consumer that
    /// expects to drain inside a single sim tick; large enough to absorb a temporary
    /// stall without losing data, small enough that a runaway producer can't grow it
    /// unbounded. Mirrors the literal default on the primary constructor; C# does not
    /// let a primary-ctor parameter default reference a const declared in the same
    /// record body, so the value lives in both places.</summary>
    public const int DefaultMaxQueuedPayloads = 2048;

    /// <summary>Minimum acceptable receive-queue ceiling. One entry would force a poll
    /// per receive which is never what callers want.</summary>
    public const int MinimumMaxQueuedPayloads = 8;

    /// <summary>Reconnect policy used by client sessions when none is supplied
    /// explicitly. Server sessions ignore this and stay bound regardless.</summary>
    public NetReconnectPolicy EffectiveReconnect => Reconnect ?? NetReconnectPolicy.Default;

    /// <summary>Throws when the options are internally inconsistent.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            throw new ArgumentException(
                "NetSessionOptions.DisplayName must not be empty.",
                nameof(DisplayName));
        }

        if (MaxQueuedPayloads < MinimumMaxQueuedPayloads)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxQueuedPayloads),
                $"NetSessionOptions.MaxQueuedPayloads must be at least {MinimumMaxQueuedPayloads}.");
        }

        if (Role == NetSessionRole.Client)
        {
            EffectiveReconnect.Validate();
        }
    }
}
