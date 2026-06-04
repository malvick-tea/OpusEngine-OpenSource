using System;
using System.Collections.Generic;
using Opus.Engine.AlphaHarness.Smoke;
using Opus.Engine.AlphaStress.FramePacing;
using Opus.Engine.AlphaStress.Stress;

namespace Opus.Engine.AlphaStress.Tests.Support;

/// <summary>Deterministic <see cref="IAlphaStressIterationRunner"/> used in unit tests.
/// Returns a queue of canned results so the harness orchestrator can be exercised
/// without standing up the D3D12 host.</summary>
internal sealed class FakeStressIterationRunner : IAlphaStressIterationRunner
{
    private readonly Queue<AlphaStressIterationRunResult> _queue;
    private readonly List<(int Index, AlphaSmokeProfile Profile)> _calls = new();

    public FakeStressIterationRunner(IEnumerable<AlphaStressIterationRunResult> results)
    {
        _queue = new Queue<AlphaStressIterationRunResult>(results);
    }

    public IReadOnlyList<(int Index, AlphaSmokeProfile Profile)> Calls => _calls;

    public AlphaStressIterationRunResult Run(int iterationIndex, AlphaSmokeProfile iterationProfile)
    {
        _calls.Add((iterationIndex, iterationProfile));
        if (_queue.Count == 0)
        {
            throw new InvalidOperationException("FakeStressIterationRunner ran out of canned results.");
        }

        return _queue.Dequeue();
    }
}
