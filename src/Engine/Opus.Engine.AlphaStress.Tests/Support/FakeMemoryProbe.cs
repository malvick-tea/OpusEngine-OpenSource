using System;
using System.Collections.Generic;
using Opus.Engine.AlphaStress.Memory;

namespace Opus.Engine.AlphaStress.Tests.Support;

/// <summary>Returns a queue of pre-baked memory samples in order. Lets the harness be
/// exercised without taking real process measurements.</summary>
internal sealed class FakeMemoryProbe : IMemoryProbe
{
    private readonly Queue<MemoryProbeSample> _queue;

    public FakeMemoryProbe(IEnumerable<MemoryProbeSample> samples)
    {
        _queue = new Queue<MemoryProbeSample>(samples);
    }

    public MemoryProbeSample Capture()
    {
        if (_queue.Count == 0)
        {
            throw new InvalidOperationException("FakeMemoryProbe ran out of canned samples.");
        }

        return _queue.Dequeue();
    }
}
