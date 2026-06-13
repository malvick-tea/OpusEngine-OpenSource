using System;

namespace Opus.Engine.AlphaStress.Network;

/// <summary>
/// Engine-neutral seam the stress harness drives once per iteration to exercise the
/// fault-injection network layer. Production callers implement this against the
/// loopback hub plus <see cref="Opus.Engine.Net.Transport.LatencyLossWrappingTransport"/>
/// (see <see cref="LoopbackFaultInjectionNetworkProbe"/>); tests implement it as a
/// deterministic fake so the harness can be exercised without standing up a real soak
/// rig.
/// </summary>
public interface IAlphaStressNetworkProbe : IDisposable
{
    /// <summary>Runs one iteration of the workload described by <paramref name="profile"/>
    /// against a freshly-built transport stack and returns the observed counters.
    /// Implementations must not throw past their public surface — failures translate to
    /// observations with non-zero soak issue counts so the harness can surface them
    /// through <c>OPDX-STR-006</c> instead of crashing the run.</summary>
    AlphaStressNetworkObservation RunIteration(int iterationIndex, AlphaStressNetworkProfile profile);
}
