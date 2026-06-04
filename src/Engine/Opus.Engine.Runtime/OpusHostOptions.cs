using Opus.Engine.Pal.Application;
using Opus.Foundation;

namespace Opus.Engine.Runtime;

/// <summary>Configuration for the deterministic core host loop.</summary>
public sealed record OpusHostOptions
{
    public const int DefaultMaxFixedTicksPerFrame = 5;

    public int TickRateHz { get; init; } = GameTime.DefaultTickRateHz;

    /// <summary>
    /// Maximum fixed ticks drained from one variable frame before excess whole ticks are
    /// dropped. Lead tuning point: M7/M11 stress runs should set the final alpha policy.
    /// </summary>
    public int MaxFixedTicksPerFrame { get; init; } = DefaultMaxFixedTicksPerFrame;

    public bool RenderWhilePaused { get; init; } = true;

    public WindowOptions? Window { get; init; }

    internal void Validate(string paramName)
    {
        if (TickRateHz <= 0)
        {
            throw new ArgumentException("TickRateHz must be positive.", paramName);
        }

        if (MaxFixedTicksPerFrame <= 0)
        {
            throw new ArgumentException("MaxFixedTicksPerFrame must be positive.", paramName);
        }
    }
}
