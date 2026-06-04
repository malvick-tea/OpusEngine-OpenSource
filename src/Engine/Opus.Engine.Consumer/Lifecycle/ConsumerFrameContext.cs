using System;
using Opus.Foundation;

namespace Opus.Engine.Consumer.Lifecycle;

/// <summary>Engine-neutral per-frame context delivered to consumer hooks and scene sources.</summary>
/// <param name="Time">Fixed-time snapshot from the Opus runtime host.</param>
/// <param name="Delta">Variable render-frame delta.</param>
/// <param name="InterpolationAlpha">Fraction between the previous and next fixed tick.</param>
/// <param name="FrameIndex">Monotonic render-frame index.</param>
/// <param name="CapturedAtUtc">UTC timestamp when the host built this context.</param>
public sealed record ConsumerFrameContext(
    GameTime Time,
    TimeSpan Delta,
    double InterpolationAlpha,
    ulong FrameIndex,
    DateTimeOffset CapturedAtUtc)
{
    /// <summary>Creates the context with UTC-normalised timestamp.</summary>
    public static ConsumerFrameContext Create(
        GameTime time,
        TimeSpan delta,
        double interpolationAlpha,
        ulong frameIndex,
        DateTimeOffset capturedAtUtc)
    {
        if (delta < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delta), "Frame delta must not be negative.");
        }

        return new ConsumerFrameContext(
            time,
            delta,
            interpolationAlpha,
            frameIndex,
            capturedAtUtc.ToUniversalTime());
    }
}
