using System;

namespace Opus.Engine.Net.Session;

/// <summary>
/// Pure computation of the next reconnect delay for a <see cref="NetReconnectPolicy"/>.
/// The schedule is stateless aside from the supplied attempt index; the session owns
/// the counter so the policy itself stays a data record.
/// </summary>
public static class NetReconnectSchedule
{
    /// <summary>
    /// Returns the delay to wait before the <paramref name="nextAttemptIndex"/>'th
    /// reconnect attempt. Attempt index is 1-based: the first retry uses
    /// <see cref="NetReconnectPolicy.BaseDelay"/>, subsequent retries grow by
    /// <see cref="NetReconnectPolicy.BackoffMultiplier"/> until the delay reaches
    /// <see cref="NetReconnectPolicy.MaxDelay"/>.
    /// </summary>
    public static TimeSpan ComputeDelay(NetReconnectPolicy policy, int nextAttemptIndex)
    {
        ArgumentNullException.ThrowIfNull(policy);
        policy.Validate();
        if (nextAttemptIndex < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nextAttemptIndex),
                "Reconnect attempt index is 1-based.");
        }

        var baseMs = policy.BaseDelay.TotalMilliseconds;
        if (baseMs <= 0)
        {
            return TimeSpan.Zero;
        }

        // Exponent stays an integer in policy.BackoffMultiplier^(n-1); we use
        // Math.Pow for the general real-valued case. Clamp upward at MaxDelay so a
        // pathological multiplier cannot overflow into double.PositiveInfinity.
        var grown = baseMs * Math.Pow(policy.BackoffMultiplier, nextAttemptIndex - 1);
        if (double.IsInfinity(grown) || grown >= policy.MaxDelay.TotalMilliseconds)
        {
            return policy.MaxDelay;
        }

        return TimeSpan.FromMilliseconds(grown);
    }

    /// <summary>True when the policy has more attempts available for the given count of
    /// already-spent attempts.</summary>
    public static bool HasBudget(NetReconnectPolicy policy, int attemptsSpent)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentOutOfRangeException.ThrowIfNegative(attemptsSpent);
        return attemptsSpent < policy.MaxAttempts;
    }
}
