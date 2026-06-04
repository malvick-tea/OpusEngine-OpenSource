using System;

namespace Opus.Foundation;

/// <summary>
/// Simulation clock. Fixed timestep, deterministic. Real wall-clock and frame-clock
/// are concerns of the host, not the simulation.
/// </summary>
public readonly record struct GameTime
{
    public const int DefaultTickRateHz = 60;

    public GameTime(Tick tick, double tickIntervalSeconds)
    {
        if (tickIntervalSeconds <= 0 || double.IsNaN(tickIntervalSeconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(tickIntervalSeconds), tickIntervalSeconds, "Tick interval must be positive.");
        }

        Tick = tick;
        TickIntervalSeconds = tickIntervalSeconds;
    }

    public Tick Tick { get; }

    public double TickIntervalSeconds { get; }

    public double Seconds => Tick.Value * TickIntervalSeconds;

    public TimeSpan Elapsed => TimeSpan.FromSeconds(Seconds);

    public GameTime Advance(int ticks = 1) => new(Tick + ticks, TickIntervalSeconds);

    public static GameTime AtRate(int hz) =>
        new(Tick.Zero, 1.0 / Math.Max(1, hz));
}
