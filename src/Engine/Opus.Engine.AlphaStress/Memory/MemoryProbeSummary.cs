using System;

namespace Opus.Engine.AlphaStress.Memory;

/// <summary>
/// Aggregated memory summary over one stress iteration or one stress run. Captures the
/// pre/post samples plus derived growth deltas and gen-2 collection delta. Plain data
/// shape so the writer can serialise it through <c>System.Text.Json</c> and so tests can
/// assert against the numbers without reaching into harness internals.
/// </summary>
/// <param name="SampleCount">Number of <see cref="MemoryProbeSample"/>s the aggregator
/// observed.</param>
/// <param name="First">First sample captured.</param>
/// <param name="Last">Last sample captured.</param>
/// <param name="PeakWorkingSetBytes">Highest observed process working set across the
/// run.</param>
/// <param name="PeakManagedHeapBytes">Highest observed managed heap size across the
/// run.</param>
/// <param name="ManagedHeapGrowthBytes">
/// <c>Last.ManagedHeapBytes - First.ManagedHeapBytes</c>; negative values are clamped to
/// zero because GC reclamation should never count as positive growth.
/// </param>
/// <param name="WorkingSetGrowthBytes">
/// <c>Last.WorkingSetBytes - First.WorkingSetBytes</c>; negative values are clamped to
/// zero for the same reason as <see cref="ManagedHeapGrowthBytes"/>.
/// </param>
/// <param name="Gen2CollectionsDelta">Number of gen-2 collections observed between
/// <see cref="First"/> and <see cref="Last"/>.</param>
public sealed record MemoryProbeSummary(
    int SampleCount,
    MemoryProbeSample? First,
    MemoryProbeSample? Last,
    long PeakWorkingSetBytes,
    long PeakManagedHeapBytes,
    long ManagedHeapGrowthBytes,
    long WorkingSetGrowthBytes,
    int Gen2CollectionsDelta)
{
    /// <summary>Empty summary — emitted when the aggregator observed zero samples.</summary>
    public static MemoryProbeSummary Empty { get; } = new(
        SampleCount: 0,
        First: null,
        Last: null,
        PeakWorkingSetBytes: 0,
        PeakManagedHeapBytes: 0,
        ManagedHeapGrowthBytes: 0,
        WorkingSetGrowthBytes: 0,
        Gen2CollectionsDelta: 0);

    /// <summary>True when at least one sample was observed.</summary>
    public bool HasSamples => SampleCount > 0;
}
