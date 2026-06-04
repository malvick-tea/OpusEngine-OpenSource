using System;
using Opus.Engine.AlphaHarness.Smoke;
using Opus.Engine.AlphaStress.FramePacing;

namespace Opus.Engine.AlphaStress.Stress;

/// <summary>
/// Outcome of one stress iteration. Captures the underlying smoke outcome (or null when
/// the harness could not produce one) plus the per-iteration frame-pacing summary the
/// observer collected for that single iteration.
/// </summary>
/// <param name="IterationIndex">Zero-based iteration index.</param>
/// <param name="StartedAtUtc">UTC timestamp the iteration started at.</param>
/// <param name="ElapsedWallClock">Wall-clock duration the iteration took.</param>
/// <param name="SmokeOutcome">Underlying smoke outcome; null when the harness could not
/// produce one (host unavailable, unhandled exception).</param>
/// <param name="FramePacing">Frame pacing summary for this iteration alone. Empty when
/// no observations were recorded.</param>
/// <param name="UnhandledExceptionMessage">When the iteration threw, the captured
/// exception message. Null on the happy path.</param>
public sealed record AlphaStressIterationOutcome(
    int IterationIndex,
    DateTimeOffset StartedAtUtc,
    TimeSpan ElapsedWallClock,
    AlphaSmokeOutcome? SmokeOutcome,
    FramePacingSummary FramePacing,
    string? UnhandledExceptionMessage)
{
    /// <summary>True when the iteration produced a clean smoke outcome and threw no
    /// unhandled exception.</summary>
    public bool IsClean => SmokeOutcome is { IsClean: true } && UnhandledExceptionMessage is null;
}
