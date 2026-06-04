using System;
using FluentAssertions;
using Opus.Engine.Consumer.Lifecycle;
using Opus.Foundation;
using Xunit;

namespace Opus.Engine.Consumer.Tests.Lifecycle;

public sealed class ConsumerFrameContextTests
{
    private static readonly DateTimeOffset NonUtcInstant =
        new(2026, 5, 27, 9, 0, 0, TimeSpan.FromHours(3));

    [Fact]
    public void Create_normalises_timestamp_to_utc()
    {
        var context = ConsumerFrameContext.Create(
            GameTime.AtRate(60).Advance(60),
            TimeSpan.FromMilliseconds(16.7),
            interpolationAlpha: 0.5,
            frameIndex: 7,
            NonUtcInstant);

        context.CapturedAtUtc.Offset.Should().Be(TimeSpan.Zero);
        context.CapturedAtUtc.Should().Be(NonUtcInstant.ToUniversalTime());
    }

    [Fact]
    public void Create_rejects_negative_delta()
    {
        Action act = () => ConsumerFrameContext.Create(
            GameTime.AtRate(60),
            TimeSpan.FromMilliseconds(-1),
            interpolationAlpha: 0,
            frameIndex: 0,
            DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*delta*");
    }

    [Fact]
    public void Started_context_rejects_null_build()
    {
        Action act = () => _ = new ConsumerLifecycleStartedContext(build: null!, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Started_context_normalises_timestamp_to_utc()
    {
        var context = new ConsumerLifecycleStartedContext(BuildInfo.Current, NonUtcInstant);

        context.StartedAtUtc.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Stopping_context_rejects_negative_frame_count()
    {
        Action act = () => _ = new ConsumerLifecycleStoppingContext(BuildInfo.Current, DateTimeOffset.UtcNow, framesObserved: -1);

        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*frame count*");
    }

    [Fact]
    public void Stopping_context_normalises_timestamp_to_utc()
    {
        var context = new ConsumerLifecycleStoppingContext(BuildInfo.Current, NonUtcInstant, framesObserved: 42);

        context.StoppingAtUtc.Offset.Should().Be(TimeSpan.Zero);
        context.FramesObserved.Should().Be(42);
    }
}
