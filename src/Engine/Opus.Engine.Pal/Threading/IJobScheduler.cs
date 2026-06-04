using System;
using System.Threading;
using System.Threading.Tasks;

namespace Opus.Engine.Pal.Threading;

/// <summary>
/// Background work scheduler. Sim never touches this — it runs single-threaded by design.
/// Engine + Game + Client use it for asset I/O, codec work, telemetry flush, etc.
/// </summary>
public interface IJobScheduler
{
    /// <summary>Number of worker threads available. Useful for sizing parallel-for chunking.</summary>
    int WorkerCount { get; }

    JobHandle Run(Action work, CancellationToken ct = default);

    JobHandle Run(Func<Task> work, CancellationToken ct = default);

    JobHandle<T> Run<T>(Func<T> work, CancellationToken ct = default);

    JobHandle<T> Run<T>(Func<Task<T>> work, CancellationToken ct = default);
}

/// <summary>Strongly-typed result variant of <see cref="JobHandle"/>.</summary>
public readonly struct JobHandle<T>
{
    private readonly Task<T> _task;

    internal JobHandle(Task<T> task)
    {
        _task = task;
    }

    public bool IsCompleted => _task is null || _task.IsCompleted;

    public Task<T> AsTask() => _task ?? Task.FromResult<T>(default!);

    public static JobHandle<T> FromTask(Task<T> task) => new(task);
}
