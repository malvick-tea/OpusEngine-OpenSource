using System;
using FluentAssertions;
using Opus.Engine.AlphaStress.FramePacing;
using Xunit;

namespace Opus.Engine.AlphaStress.Tests.FramePacing;

public sealed class FramePacingObservationTests
{
    [Fact]
    public void Validate_accepts_canonical_observation()
    {
        var observation = new FramePacingObservation(
            FrameNumber: 1,
            ObservedAtUtc: DateTimeOffset.UtcNow,
            CpuFrameTime: TimeSpan.FromMilliseconds(16.7));

        var act = observation.Validate;

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_rejects_non_positive_frame_number(long frameNumber)
    {
        var observation = new FramePacingObservation(
            FrameNumber: frameNumber,
            ObservedAtUtc: DateTimeOffset.UtcNow,
            CpuFrameTime: TimeSpan.FromMilliseconds(16.7));

        var act = observation.Validate;

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("FrameNumber");
    }

    [Fact]
    public void Validate_rejects_negative_cpu_frame_time()
    {
        var observation = new FramePacingObservation(
            FrameNumber: 1,
            ObservedAtUtc: DateTimeOffset.UtcNow,
            CpuFrameTime: TimeSpan.FromMilliseconds(-1));

        var act = observation.Validate;

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("CpuFrameTime");
    }

    [Fact]
    public void Validate_accepts_zero_cpu_frame_time()
    {
        var observation = new FramePacingObservation(
            FrameNumber: 1,
            ObservedAtUtc: DateTimeOffset.UtcNow,
            CpuFrameTime: TimeSpan.Zero);

        var act = observation.Validate;

        act.Should().NotThrow();
    }
}
