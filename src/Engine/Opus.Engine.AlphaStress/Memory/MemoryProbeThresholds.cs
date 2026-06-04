using System;

namespace Opus.Engine.AlphaStress.Memory;

/// <summary>
/// Engine-neutral thresholds the stress harness applies against an aggregated
/// <see cref="MemoryProbeSummary"/>. A summary exceeding any threshold marks the
/// stress run as <c>StressMemoryGrowthExceeded</c> (<c>OPDX-STR-005</c>).
/// </summary>
/// <param name="ManagedHeapGrowthLimitBytes">Maximum allowed growth in
/// <see cref="MemoryProbeSummary.ManagedHeapGrowthBytes"/>. Defaults to 64 MiB —
/// roughly the M5.1 alpha-host steady-state managed footprint, so growth past this
/// limit during a 20-player stress run flags a real leak rather than warmup churn.</param>
/// <param name="WorkingSetGrowthLimitBytes">Maximum allowed growth in
/// <see cref="MemoryProbeSummary.WorkingSetGrowthBytes"/>. Defaults to 256 MiB —
/// the headroom we allow for GPU resource churn (textures, swap-chain rebuilds) on
/// top of the managed footprint without flagging an alpha-blocking leak.</param>
/// <param name="Gen2CollectionLimit">Maximum allowed number of gen-2 collections during
/// a stress run. Defaults to four — a healthy 20-player stress should see only a small
/// handful of gen-2 GCs; a sustained pattern indicates pressure on the LOH or a
/// reference cycle worth surfacing.</param>
public sealed record MemoryProbeThresholds(
    long ManagedHeapGrowthLimitBytes,
    long WorkingSetGrowthLimitBytes,
    int Gen2CollectionLimit)
{
    /// <summary>64 MiB.</summary>
    public const long DefaultManagedHeapGrowthLimitBytes = 64L * 1024 * 1024;

    /// <summary>256 MiB.</summary>
    public const long DefaultWorkingSetGrowthLimitBytes = 256L * 1024 * 1024;

    /// <summary>4 gen-2 collections across the run.</summary>
    public const int DefaultGen2CollectionLimit = 4;

    /// <summary>Default thresholds calibrated for the M11 20-player stress shape.</summary>
    public static MemoryProbeThresholds Default { get; } = new(
        ManagedHeapGrowthLimitBytes: DefaultManagedHeapGrowthLimitBytes,
        WorkingSetGrowthLimitBytes: DefaultWorkingSetGrowthLimitBytes,
        Gen2CollectionLimit: DefaultGen2CollectionLimit);

    /// <summary>Throws when the thresholds are internally inconsistent.</summary>
    public void Validate()
    {
        if (ManagedHeapGrowthLimitBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ManagedHeapGrowthLimitBytes),
                "ManagedHeapGrowthLimitBytes must be positive.");
        }

        if (WorkingSetGrowthLimitBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(WorkingSetGrowthLimitBytes),
                "WorkingSetGrowthLimitBytes must be positive.");
        }

        if (Gen2CollectionLimit < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Gen2CollectionLimit),
                "Gen2CollectionLimit must be non-negative.");
        }
    }
}
