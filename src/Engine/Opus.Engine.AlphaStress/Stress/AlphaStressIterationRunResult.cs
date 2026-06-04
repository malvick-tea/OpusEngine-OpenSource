using System;
using System.Collections.Generic;
using Opus.Engine.AlphaHarness.Smoke;
using Opus.Engine.AlphaStress.FramePacing;

namespace Opus.Engine.AlphaStress.Stress;

/// <summary>
/// Result returned by an <see cref="IAlphaStressIterationRunner"/> for one iteration.
/// Carries the underlying smoke outcome (or null when the host could not be opened),
/// the per-frame pacing observations captured during the iteration, and the optional
/// exception the iteration threw past its public surface.
/// </summary>
/// <param name="SmokeOutcome">Underlying smoke outcome; null when the host was
/// unavailable.</param>
/// <param name="FramePacingObservations">Per-frame CPU pacing observations the host
/// captured during the iteration. Must be sorted by frame number ascending. Empty when
/// no frames were observed.</param>
/// <param name="UnhandledException">Exception the iteration runner caught at its own
/// boundary; null on the happy path. The harness translates this into the run-wide
/// <see cref="AlphaStressIssueCode.IterationUnhandledException"/> issue.</param>
public sealed record AlphaStressIterationRunResult(
    AlphaSmokeOutcome? SmokeOutcome,
    IReadOnlyList<FramePacingObservation> FramePacingObservations,
    Exception? UnhandledException);
