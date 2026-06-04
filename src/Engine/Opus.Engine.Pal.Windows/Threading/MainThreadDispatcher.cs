using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Opus.Engine.Pal.Threading;
using Opus.Foundation;

namespace Opus.Engine.Pal.Windows.Threading;

/// <summary>
/// Pumps queued callbacks onto the main thread. Constructed by <c>WindowsEntry</c> on
/// the very first line of <c>Main()</c> so the captured thread id is always the genuine
/// main thread (not a worker that happens to call this first).
///
/// Drain it once per frame from the host loop via <see cref="DrainPending"/>.
/// </summary>
public sealed class MainThreadDispatcher : IMainThreadDispatcher
{
    private readonly int _mainThreadId;
    private readonly ConcurrentQueue<Action> _queue = new();

    public MainThreadDispatcher()
    {
        _mainThreadId = Environment.CurrentManagedThreadId;
    }

    public bool IsOnMainThread => Environment.CurrentManagedThreadId == _mainThreadId;

    public void Post(Action callback)
    {
        Ensure.NotNull(callback);
        _queue.Enqueue(callback);
    }

    public Task InvokeAsync(Action callback)
    {
        Ensure.NotNull(callback);

        if (IsOnMainThread)
        {
            callback();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Enqueue(() =>
        {
            try
            {
                callback();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    public Task<T> InvokeAsync<T>(Func<T> callback)
    {
        Ensure.NotNull(callback);

        if (IsOnMainThread)
        {
            return Task.FromResult(callback());
        }

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Enqueue(() =>
        {
            try
            {
                tcs.SetResult(callback());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    /// <summary>Runs every queued callback. Call once per frame from the main loop.</summary>
    public void DrainPending()
    {
        while (_queue.TryDequeue(out var cb))
        {
            cb();
        }
    }
}
