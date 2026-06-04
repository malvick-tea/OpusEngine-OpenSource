using System;

namespace Opus.Engine.Net.Session;

/// <summary>
/// Reconnect cadence for a client <see cref="NetSession"/>. All fields are stored as
/// data; the actual schedule is computed by <see cref="NetReconnectSchedule"/> so the
/// policy stays a pure record.
/// </summary>
/// <param name="MaxAttempts">
/// Maximum number of reconnect attempts before the session transitions to
/// <see cref="NetSessionState.Faulted"/>. A value of <c>0</c> disables reconnect entirely:
/// the first disconnect ends the session.
/// </param>
/// <param name="BaseDelay">
/// Delay before the first reconnect attempt. Must be non-negative; <c>TimeSpan.Zero</c>
/// means "retry immediately".
/// </param>
/// <param name="MaxDelay">
/// Upper bound on the exponentially growing delay. Must be greater than or equal to
/// <see cref="BaseDelay"/>; once the computed delay reaches this bound it stays there.
/// </param>
/// <param name="BackoffMultiplier">
/// Factor by which the delay grows after each unsuccessful attempt (1.0 means linear /
/// constant; 2.0 means doubling). Must be at least <c>1.0</c>.
/// </param>
public sealed record NetReconnectPolicy(
    int MaxAttempts,
    TimeSpan BaseDelay,
    TimeSpan MaxDelay,
    double BackoffMultiplier)
{
    /// <summary>Hard floor for <see cref="BackoffMultiplier"/>: monotonic growth requires
    /// a multiplier of at least 1.0. Sub-unity values would shrink the delay between
    /// attempts which is never the desired shape.</summary>
    public const double MinimumBackoffMultiplier = 1.0;

    /// <summary>Reconnect disabled — the first disconnect ends the session immediately.</summary>
    public static NetReconnectPolicy Disabled { get; } =
        new(MaxAttempts: 0, BaseDelay: TimeSpan.Zero, MaxDelay: TimeSpan.Zero, BackoffMultiplier: MinimumBackoffMultiplier);

    /// <summary>Default alpha-grade policy: 5 attempts with exponential backoff starting
    /// at 250 ms and capping at 4 s. Sized so a tester hitting a flaky access point
    /// recovers without manual intervention but a permanently broken server still fails
    /// reasonably fast.</summary>
    public static NetReconnectPolicy Default { get; } = new(
        MaxAttempts: 5,
        BaseDelay: TimeSpan.FromMilliseconds(250),
        MaxDelay: TimeSpan.FromSeconds(4),
        BackoffMultiplier: 2.0);

    /// <summary>Throws when the policy is internally inconsistent.</summary>
    public void Validate()
    {
        if (MaxAttempts < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxAttempts),
                "Reconnect policy MaxAttempts must be non-negative.");
        }

        if (BaseDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(BaseDelay),
                "Reconnect policy BaseDelay must be non-negative.");
        }

        if (MaxDelay < BaseDelay)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxDelay),
                "Reconnect policy MaxDelay must be greater than or equal to BaseDelay.");
        }

        if (double.IsNaN(BackoffMultiplier) || BackoffMultiplier < MinimumBackoffMultiplier)
        {
            throw new ArgumentOutOfRangeException(
                nameof(BackoffMultiplier),
                $"Reconnect policy BackoffMultiplier must be >= {MinimumBackoffMultiplier}.");
        }
    }
}
