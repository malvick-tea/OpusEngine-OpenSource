using FluentAssertions;
using Opus.Engine.AlphaStress.Memory;
using Opus.Engine.AlphaStress.Tests.Support;
using Xunit;

namespace Opus.Engine.AlphaStress.Tests.Memory;

public sealed class SystemMemoryProbeTests
{
    [Fact]
    public void Capture_returns_non_negative_baseline()
    {
        var probe = new SystemMemoryProbe();

        var sample = probe.Capture();

        sample.ManagedHeapBytes.Should().BePositive();
        sample.WorkingSetBytes.Should().BePositive();
        sample.Gen0Collections.Should().BeGreaterThanOrEqualTo(0);
        sample.Gen1Collections.Should().BeGreaterThanOrEqualTo(0);
        sample.Gen2Collections.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Capture_uses_supplied_time_provider()
    {
        var time = new FakeTimeProvider();
        var probe = new SystemMemoryProbe(time);

        var sample = probe.Capture();

        sample.ObservedAtUtc.Should().Be(time.GetUtcNow());
    }
}
