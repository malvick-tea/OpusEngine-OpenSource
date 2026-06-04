using Opus.Engine.Pal.Application;

namespace Opus.Engine.Runtime;

/// <summary>Stable services available to host lifecycle callbacks.</summary>
public sealed class OpusHostContext
{
    internal OpusHostContext(
        OpusHostOptions options,
        IWindowService? window,
        ILifecycleService? lifecycle)
    {
        Options = options;
        Window = window;
        Lifecycle = lifecycle;
    }

    public OpusHostOptions Options { get; }

    public IWindowService? Window { get; }

    public ILifecycleService? Lifecycle { get; }
}
