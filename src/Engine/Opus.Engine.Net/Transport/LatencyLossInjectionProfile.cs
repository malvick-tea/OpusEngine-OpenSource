using System;

namespace Opus.Engine.Net.Transport;

/// <summary>
/// Deterministic fault-injection profile applied by <see cref="LatencyLossWrappingTransport"/>.
/// Captures the send-side packet-loss probability, the added send latency, and the seed
/// used to drive the deterministic RNG so two stress runs with the same profile produce
/// identical drop patterns. The profile is engine-neutral data: the wrapping transport
/// reads it once at construction and never mutates it.
/// </summary>
/// <param name="LossRate">Probability in <c>[0,1]</c> that any individual outbound
/// datagram is silently dropped. <c>0</c> disables outbound loss injection.</param>
/// <param name="AddedLatency">Synthetic delay applied to outbound datagrams before they
/// reach the inner transport. <see cref="TimeSpan.Zero"/> disables outbound latency
/// injection.</param>
/// <param name="Seed">RNG seed for the deterministic <see cref="Random"/> the wrapping
/// transport uses for outbound loss decisions. Pinning the seed lets a tester reproduce
/// a failing stress run bit-for-bit.</param>
public sealed record LatencyLossInjectionProfile(
    double LossRate,
    TimeSpan AddedLatency,
    int Seed)
{
    /// <summary>Probability in <c>[0,1]</c> that any individual inbound
    /// <see cref="Opus.Net.Transport.NetEventKind.Received"/> event observed by
    /// <see cref="LatencyLossWrappingTransport.Poll"/> is dropped before reaching the
    /// caller's event list. Default <c>0</c> disables inbound loss injection so the
    /// existing M11.1 send-only path remains the no-op shape.</summary>
    public double InboundLossRate { get; init; }

    /// <summary>Synthetic delay applied to inbound <c>Received</c> events before they
    /// are surfaced to the caller of <see cref="LatencyLossWrappingTransport.Poll"/>.
    /// Default <see cref="TimeSpan.Zero"/> disables inbound latency injection. Inbound
    /// control-plane events (<c>Connected</c> / <c>Disconnected</c>) are never delayed —
    /// they always flow straight through so peer-state telemetry stays accurate.</summary>
    public TimeSpan InboundAddedLatency { get; init; }

    /// <summary>Independent RNG seed for the inbound deterministic
    /// <see cref="Random"/>. Kept separate from <see cref="Seed"/> so a tester can
    /// reproduce asymmetric drop patterns where outbound and inbound diverge. Default
    /// <c>0</c>.</summary>
    public int InboundSeed { get; init; }

    /// <summary>Default profile — no loss, no latency, fixed seed. Equivalent to a
    /// pass-through wrapper in both directions.</summary>
    public static LatencyLossInjectionProfile None { get; } = new(
        LossRate: 0.0,
        AddedLatency: TimeSpan.Zero,
        Seed: 0);

    /// <summary>Throws when the profile is internally inconsistent.</summary>
    public void Validate()
    {
        if (double.IsNaN(LossRate) || LossRate < 0.0 || LossRate > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(LossRate),
                "LossRate must be in the inclusive range [0, 1].");
        }

        if (AddedLatency < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(AddedLatency),
                "AddedLatency must be non-negative.");
        }

        if (double.IsNaN(InboundLossRate) || InboundLossRate < 0.0 || InboundLossRate > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(InboundLossRate),
                "InboundLossRate must be in the inclusive range [0, 1].");
        }

        if (InboundAddedLatency < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(InboundAddedLatency),
                "InboundAddedLatency must be non-negative.");
        }
    }
}
