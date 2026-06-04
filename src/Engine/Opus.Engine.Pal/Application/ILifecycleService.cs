using System;

namespace Opus.Engine.Pal.Application;

/// <summary>
/// OS-level lifecycle signals. On desktop most events are no-ops, but mobile platforms
/// rely heavily on suspend/resume/foreground/background transitions to free resources,
/// pause input, throttle framerate, and persist state.
/// </summary>
public interface ILifecycleService
{
    /// <summary>Current state — services consult this on the main thread.</summary>
    LifecycleState State { get; }

    /// <summary>Fired whenever <see cref="State"/> changes. Subscribers run on the main thread.</summary>
    event Action<LifecycleState, LifecycleState>? StateChanged;

    /// <summary>Fired before the process exits. Last chance to flush logs and save data.</summary>
    event Action? ShuttingDown;
}

public enum LifecycleState
{
    Starting,
    Foreground,
    Background,
    Suspended,
    ShuttingDown,
}
