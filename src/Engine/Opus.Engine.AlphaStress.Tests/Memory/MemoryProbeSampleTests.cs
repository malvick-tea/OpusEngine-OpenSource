using System;
using FluentAssertions;
using Opus.Engine.AlphaStress.Memory;
using Xunit;

namespace Opus.Engine.AlphaStress.Tests.Memory;

public sealed class MemoryProbeSampleTests
{
    [Fact]
    public void Validate_accepts_canonical_sample()
    {
        var sample = new MemoryProbeSample(
            ObservedAtUtc: DateTimeOffset.UtcNow,
            ManagedHeapBytes: 1024,
            WorkingSetBytes: 4096,
            Gen0Collections: 1,
            Gen1Collections: 1,
            Gen2Collections: 0);

        var act = sample.Validate;

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(-1L, 0L, 0, 0, 0, "ManagedHeapBytes")]
    [InlineData(0L, -1L, 0, 0, 0, "WorkingSetBytes")]
    [InlineData(0L, 0L, -1, 0, 0, "Gen0Collections")]
    [InlineData(0L, 0L, 0, -1, 0, "Gen1Collections")]
    [InlineData(0L, 0L, 0, 0, -1, "Gen2Collections")]
    public void Validate_rejects_negative_fields(long managed, long workingSet, int gen0, int gen1, int gen2, string parameter)
    {
        var sample = new MemoryProbeSample(
            ObservedAtUtc: DateTimeOffset.UtcNow,
            ManagedHeapBytes: managed,
            WorkingSetBytes: workingSet,
            Gen0Collections: gen0,
            Gen1Collections: gen1,
            Gen2Collections: gen2);

        var act = sample.Validate;

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(parameter);
    }
}
