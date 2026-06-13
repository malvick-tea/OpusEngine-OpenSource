using System;

namespace Opus.Engine.AlphaStress.Memory;

/// <summary>
/// Production <see cref="IMemoryProbe"/> implementation. Reads
/// <see cref="GC.GetTotalMemory"/> with <c>forceFullCollection: false</c>, the current
/// process working set via <see cref="Environment.WorkingSet"/>, and the per-generation
/// collection counters. A single observation completes in well under a millisecond on
/// the alpha hardware so the harness can call it between iterations without distorting
/// wall-clock budgets.
/// </summary>
public sealed class SystemMemoryProbe : IMemoryProbe
{
    private readonly TimeProvider _time;

    /// <summary>Creates a probe using the supplied time source (defaults to
    /// <see cref="TimeProvider.System"/>).</summary>
    public SystemMemoryProbe(TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public MemoryProbeSample Capture() => new(
        ObservedAtUtc: _time.GetUtcNow(),
        ManagedHeapBytes: GC.GetTotalMemory(forceFullCollection: false),
        WorkingSetBytes: Environment.WorkingSet,
        Gen0Collections: GC.CollectionCount(0),
        Gen1Collections: GC.CollectionCount(1),
        Gen2Collections: GC.CollectionCount(2));
}
