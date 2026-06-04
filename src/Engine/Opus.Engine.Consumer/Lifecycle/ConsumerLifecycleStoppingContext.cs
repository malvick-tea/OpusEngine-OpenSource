using System;
using Opus.Foundation;

namespace Opus.Engine.Consumer.Lifecycle;

/// <summary>Context delivered to consumer lifecycle hooks when the host is stopping.</summary>
public sealed record ConsumerLifecycleStoppingContext
{
    /// <summary>Creates a stopping context.</summary>
    public ConsumerLifecycleStoppingContext(BuildInfo build, DateTimeOffset stoppingAtUtc, long framesObserved)
    {
        ArgumentNullException.ThrowIfNull(build);
        if (framesObserved < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(framesObserved), "Observed frame count must not be negative.");
        }

        Build = build;
        StoppingAtUtc = stoppingAtUtc.ToUniversalTime();
        FramesObserved = framesObserved;
    }

    /// <summary>Build identity for the stopping host.</summary>
    public BuildInfo Build { get; }

    /// <summary>UTC timestamp when the host dispatched stopping.</summary>
    public DateTimeOffset StoppingAtUtc { get; }

    /// <summary>Total frames observed by the host metrics surface.</summary>
    public long FramesObserved { get; }
}
