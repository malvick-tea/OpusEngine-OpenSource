using System;

namespace Opus.Engine.AlphaStress.Memory;

/// <summary>
/// Single memory snapshot the probe takes between stress iterations. Captures both
/// managed-heap size and process working set so a leak that only shows up under
/// native-resource pressure (textures, GPU buffers) is visible alongside a managed
/// leak. The aggregator derives growth deltas and gen0/1/2 collection counts from a
/// stream of these samples.
/// </summary>
/// <param name="ObservedAtUtc">UTC timestamp the probe captured the sample at.</param>
/// <param name="ManagedHeapBytes">Live managed heap bytes reported by
/// <c>GC.GetTotalMemory(false)</c>.</param>
/// <param name="WorkingSetBytes">Process working-set bytes reported by
/// <c>Environment.WorkingSet</c>.</param>
/// <param name="Gen0Collections">Gen-0 collection count since process start.</param>
/// <param name="Gen1Collections">Gen-1 collection count since process start.</param>
/// <param name="Gen2Collections">Gen-2 collection count since process start.</param>
public sealed record MemoryProbeSample(
    DateTimeOffset ObservedAtUtc,
    long ManagedHeapBytes,
    long WorkingSetBytes,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections)
{
    /// <summary>Throws when the sample is internally inconsistent.</summary>
    public void Validate()
    {
        if (ManagedHeapBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ManagedHeapBytes), "ManagedHeapBytes must be non-negative.");
        }

        if (WorkingSetBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(WorkingSetBytes), "WorkingSetBytes must be non-negative.");
        }

        if (Gen0Collections < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Gen0Collections), "Gen0Collections must be non-negative.");
        }

        if (Gen1Collections < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Gen1Collections), "Gen1Collections must be non-negative.");
        }

        if (Gen2Collections < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Gen2Collections), "Gen2Collections must be non-negative.");
        }
    }
}
