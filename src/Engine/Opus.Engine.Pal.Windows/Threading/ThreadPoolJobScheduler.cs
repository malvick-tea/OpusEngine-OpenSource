using System;
using System.Threading;
using System.Threading.Tasks;
using Opus.Engine.Pal.Threading;
using Opus.Foundation;

namespace Opus.Engine.Pal.Windows.Threading;

/// <summary>
/// Default scheduler — wraps the .NET thread pool. AAA workloads with predictable
/// frame budgets should later swap this for a fibre/worker scheduler with affinity
/// (per layout v2 §7), but for M1 this gets us off the ground.
/// </summary>
public sealed class ThreadPoolJobScheduler : IJobScheduler
{
    public int WorkerCount => Environment.ProcessorCount;

    public JobHandle Run(Action work, CancellationToken ct = default)
    {
        Ensure.NotNull(work);
        var task = Task.Run(work, ct);
        return JobHandle.FromTask(task);
    }

    public JobHandle Run(Func<Task> work, CancellationToken ct = default)
    {
        Ensure.NotNull(work);
        var task = Task.Run(work, ct);
        return JobHandle.FromTask(task);
    }

    public JobHandle<T> Run<T>(Func<T> work, CancellationToken ct = default)
    {
        Ensure.NotNull(work);
        var task = Task.Run(work, ct);
        return JobHandle<T>.FromTask(task);
    }

    public JobHandle<T> Run<T>(Func<Task<T>> work, CancellationToken ct = default)
    {
        Ensure.NotNull(work);
        var task = Task.Run(work, ct);
        return JobHandle<T>.FromTask(task);
    }
}
