using System;

namespace Opus.Engine.Net.Session;

/// <summary>
/// Owns the reconnect attempt counter and countdown for a client <see cref="NetSession"/>.
/// Extracted from <see cref="NetSession"/> so the session orchestrator stays within the
/// tutorial's class-size budget and the reconnect cadence has a single home that can be
/// unit-tested in isolation.
/// </summary>
internal sealed class NetSessionReconnectController
{
    private readonly NetReconnectPolicy _policy;
    private TimeSpan _countdown;
    private int _attempts;

    public NetSessionReconnectController(NetReconnectPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        policy.Validate();
        _policy = policy;
    }

    /// <summary>Total reconnect attempts already launched.</summary>
    public int Attempts => _attempts;

    /// <summary>True when at least one further attempt is allowed.</summary>
    public bool HasBudget => NetReconnectSchedule.HasBudget(_policy, _attempts);

    /// <summary>True when the countdown has elapsed and the next attempt should fire.</summary>
    public bool ShouldFireNow => _countdown <= TimeSpan.Zero;

    /// <summary>Length of the most recently scheduled countdown.</summary>
    public TimeSpan LastScheduledDelay { get; private set; }

    /// <summary>Resets the controller to the post-Start state.</summary>
    public void Reset()
    {
        _attempts = 0;
        _countdown = TimeSpan.Zero;
        LastScheduledDelay = TimeSpan.Zero;
    }

    /// <summary>Schedules the next attempt's countdown based on the policy and the
    /// number of attempts already spent. Returns the configured delay.</summary>
    public TimeSpan Schedule()
    {
        var delay = NetReconnectSchedule.ComputeDelay(_policy, _attempts + 1);
        _countdown = delay;
        LastScheduledDelay = delay;
        return delay;
    }

    /// <summary>Advances the countdown by <paramref name="elapsed"/>.</summary>
    public void Advance(TimeSpan elapsed)
    {
        if (elapsed > TimeSpan.Zero)
        {
            _countdown -= elapsed;
        }
    }

    /// <summary>Marks one reconnect attempt as launched and returns the new attempt
    /// number (1-based).</summary>
    public int RecordAttempt()
    {
        _attempts++;
        return _attempts;
    }
}
