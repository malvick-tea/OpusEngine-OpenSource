using System;
using Opus.Engine.Pal.Application;

namespace Opus.Engine.Pal.Windows.Application;

/// <summary>
/// Desktop Windows is mostly stateless from a lifecycle POV — the app is either running
/// or shutting down. We still raise <see cref="StateChanged"/> on transitions so client
/// code can wire in the same way as on mobile.
/// </summary>
public sealed class WindowsLifecycleService : ILifecycleService, IDisposable
{
    private LifecycleState _state = LifecycleState.Starting;
    private bool _disposed;

    public LifecycleState State => _state;

    public event Action<LifecycleState, LifecycleState>? StateChanged;

    public event Action? ShuttingDown;

    public void EnterForeground()
    {
        Transition(LifecycleState.Foreground);
    }

    public void EnterBackground()
    {
        Transition(LifecycleState.Background);
    }

    public void BeginShutdown()
    {
        if (_state == LifecycleState.ShuttingDown)
        {
            return;
        }

        Transition(LifecycleState.ShuttingDown);
        ShuttingDown?.Invoke();
    }

    private void Transition(LifecycleState next)
    {
        if (_state == next)
        {
            return;
        }

        var prev = _state;
        _state = next;
        StateChanged?.Invoke(prev, next);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        BeginShutdown();
        _disposed = true;
    }
}
