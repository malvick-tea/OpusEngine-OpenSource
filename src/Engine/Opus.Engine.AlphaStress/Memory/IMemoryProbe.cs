namespace Opus.Engine.AlphaStress.Memory;

/// <summary>
/// Engine-neutral memory snapshot factory. Wraps the <see cref="System.GC"/> + process
/// APIs the stress harness uses to capture <see cref="MemoryProbeSample"/> between
/// iterations. Exposed as an interface so tests can substitute a deterministic probe;
/// runtime callers use <see cref="SystemMemoryProbe"/> for the live .NET / process
/// counters.
/// </summary>
public interface IMemoryProbe
{
    /// <summary>Captures one sample. Implementations must not throw on the happy path —
    /// the stress harness drives this on the iteration boundary and a throw would crash
    /// the whole stress run.</summary>
    MemoryProbeSample Capture();
}
