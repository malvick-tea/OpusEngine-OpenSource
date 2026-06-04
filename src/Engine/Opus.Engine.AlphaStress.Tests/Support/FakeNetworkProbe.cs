using System;
using System.Collections.Generic;
using Opus.Engine.AlphaStress.Network;

namespace Opus.Engine.AlphaStress.Tests.Support;

/// <summary>Deterministic <see cref="IAlphaStressNetworkProbe"/> used by stress harness
/// tests. Returns the scripted observations in order; records every invocation so tests
/// can assert how many iterations the harness drove the probe across.</summary>
public sealed class FakeNetworkProbe : IAlphaStressNetworkProbe
{
    private readonly Queue<AlphaStressNetworkObservation> _scripted;

    public FakeNetworkProbe(IEnumerable<AlphaStressNetworkObservation> scripted)
    {
        _scripted = new Queue<AlphaStressNetworkObservation>(scripted);
    }

    public List<int> Calls { get; } = new();

    public bool Disposed { get; private set; }

    public AlphaStressNetworkObservation RunIteration(int iterationIndex, AlphaStressNetworkProfile profile)
    {
        Calls.Add(iterationIndex);
        return _scripted.Count == 0
            ? new AlphaStressNetworkObservation(
                IterationIndex: iterationIndex,
                ObservedAtUtc: DateTimeOffset.UtcNow,
                ClientSendAttempts: 0,
                DroppedPackets: 0,
                DelayedPackets: 0,
                SoakIssueCount: 0)
            : _scripted.Dequeue();
    }

    public void Dispose() => Disposed = true;
}
