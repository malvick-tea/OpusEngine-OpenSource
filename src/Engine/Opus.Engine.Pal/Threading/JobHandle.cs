using System;
using System.Threading.Tasks;

namespace Opus.Engine.Pal.Threading;

/// <summary>
/// Opaque ticket for a job submitted to <see cref="IJobScheduler"/>. Wraps a
/// <see cref="Task"/> to integrate with async/await without leaking the scheduler choice.
/// </summary>
public readonly struct JobHandle : IEquatable<JobHandle>
{
    private readonly Task _task;

    internal JobHandle(Task task)
    {
        _task = task;
    }

    public bool IsCompleted => _task is null || _task.IsCompleted;

    public bool IsFaulted => _task is { IsFaulted: true };

    public Exception? Error => _task?.Exception;

    public Task AsTask() => _task ?? Task.CompletedTask;

    public static JobHandle FromTask(Task task) => new(task);

    public static JobHandle Completed => new(Task.CompletedTask);

    public bool Equals(JobHandle other) => ReferenceEquals(_task, other._task);

    public override bool Equals(object? obj) => obj is JobHandle h && Equals(h);

    public override int GetHashCode() => _task is null ? 0 : _task.GetHashCode();

    public static bool operator ==(JobHandle a, JobHandle b) => a.Equals(b);

    public static bool operator !=(JobHandle a, JobHandle b) => !a.Equals(b);
}
