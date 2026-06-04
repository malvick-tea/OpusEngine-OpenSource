using System;

namespace Opus.Engine.AlphaStress.Tests.Support;

/// <summary>Deterministic time source used by AlphaStress tests so observed timestamps
/// stay reproducible without relying on real-clock skew.</summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public FakeTimeProvider()
    {
        _now = new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);
    }

    public void Advance(TimeSpan delta) => _now += delta;

    public override DateTimeOffset GetUtcNow() => _now;
}
