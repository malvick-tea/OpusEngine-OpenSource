using System;
using Opus.Engine.Pal.Application;

namespace Opus.Engine.Pal.Windows.Application;

/// <summary>Thread-safe lifecycle state for a desktop Windows host.</summary>
public sealed class WindowsLifecycleService : ILifecycleService, IDisposable
{
    private readonly object _sync = new();
    private LifecycleState _state = LifecycleState.Starting;
    private bool _disposed;

    public LifecycleState State
    {
        get
        {
            lock (_sync)
            {
                return _state;
            }
        }
    }

    public event Action<LifecycleState, LifecycleState>? StateChanged;

    public event Action? ShuttingDown;

    public void EnterForeground() => Transition(LifecycleState.Foreground);

    public void EnterBackground() => Transition(LifecycleState.Background);

    public void BeginShutdown()
    {
        Action<LifecycleState, LifecycleState>? stateChanged;
        Action? shuttingDown;
        LifecycleState previous;
        lock (_sync)
        {
            if (_state == LifecycleState.ShuttingDown)
            {
                return;
            }

            previous = _state;
            _state = LifecycleState.ShuttingDown;
            stateChanged = StateChanged;
            shuttingDown = ShuttingDown;
        }

        stateChanged?.Invoke(previous, LifecycleState.ShuttingDown);
        shuttingDown?.Invoke();
    }

    private void Transition(LifecycleState next)
    {
        Action<LifecycleState, LifecycleState>? stateChanged;
        LifecycleState previous;
        lock (_sync)
        {
            if (_disposed || _state == LifecycleState.ShuttingDown || _state == next)
            {
                return;
            }

            previous = _state;
            _state = next;
            stateChanged = StateChanged;
        }

        stateChanged?.Invoke(previous, next);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        BeginShutdown();
    }
}
