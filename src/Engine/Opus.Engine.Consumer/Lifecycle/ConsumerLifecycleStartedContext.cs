using System;
using Opus.Foundation;

namespace Opus.Engine.Consumer.Lifecycle;

/// <summary>Context delivered to consumer lifecycle hooks after the host starts.</summary>
public sealed record ConsumerLifecycleStartedContext
{
    /// <summary>Creates a startup context.</summary>
    public ConsumerLifecycleStartedContext(BuildInfo build, DateTimeOffset startedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(build);
        Build = build;
        StartedAtUtc = startedAtUtc.ToUniversalTime();
    }

    /// <summary>Build identity for the running host.</summary>
    public BuildInfo Build { get; }

    /// <summary>UTC timestamp when the host dispatched startup.</summary>
    public DateTimeOffset StartedAtUtc { get; }
}
