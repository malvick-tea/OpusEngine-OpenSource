using System;

namespace Opus.Engine.AlphaHarness.Smoke;

/// <summary>
/// Knobs for an alpha-host smoke run. The smoke runner reads the record once and never
/// mutates it; every value is data, not behaviour. Defaults are calibrated for the M5.1
/// alpha-frame budget (~33 ms at 30 Hz) so a CI smoke completes inside a few seconds
/// without spending a tester slot.
/// </summary>
/// <param name="FrameTarget">Number of <see cref="OpusHostStep"/> calls the runner must
/// observe before declaring success. Must be at least one — a smoke that never renders is
/// not a smoke.</param>
/// <param name="FrameDelta">Synthetic per-frame delta passed into the host. Used so a
/// deterministic CI run does not depend on wall-clock pacing.</param>
/// <param name="WallClockBudget">Maximum wall-clock duration the smoke is allowed before
/// the runner records a <see cref="AlphaSmokeIssueCode.BudgetExceeded"/> issue.</param>
/// <param name="CapturesScreenshot">When true the runner asks the host to capture a PNG
/// at <paramref name="ScreenshotFrameIndex"/>. <c>false</c> means screenshot evidence is
/// not part of this smoke.</param>
/// <param name="ScreenshotFrameIndex">Zero-based frame index at which the smoke runner
/// asks the host for a screenshot. Ignored when <see cref="CapturesScreenshot"/> is
/// false; required to be inside <c>[0, FrameTarget)</c> otherwise.</param>
/// <param name="SmokeName">Stable display name surfaced in smoke report headers and log
/// lines so paired evidence files identify the run without grep-and-guess.</param>
public sealed record AlphaSmokeProfile(
    int FrameTarget,
    TimeSpan FrameDelta,
    TimeSpan WallClockBudget,
    bool CapturesScreenshot,
    int ScreenshotFrameIndex,
    string SmokeName)
{
    /// <summary>Minimum allowed frame target. Zero or negative values would silently let
    /// the smoke complete without exercising the host.</summary>
    public const int MinimumFrameTarget = 1;

    /// <summary>Maximum frame target. Sized so a manual or CI smoke completes in tens of
    /// seconds rather than hours; production stress runs belong to M11 stress harnesses.</summary>
    public const int MaximumFrameTarget = 10_000;

    /// <summary>Conventional smoke frame target (~one second at 60 Hz). Long enough to
    /// catch a black-window regression, short enough to ship in CI.</summary>
    public const int DefaultFrameTarget = 60;

    /// <summary>Default canonical smoke profile: 60 frames @ ~16.7 ms, three-second wall
    /// budget, no screenshot. Hosts opt into screenshot evidence via
    /// <see cref="WithScreenshot"/>.</summary>
    public static AlphaSmokeProfile Default { get; } = new(
        FrameTarget: DefaultFrameTarget,
        FrameDelta: TimeSpan.FromMilliseconds(16.7),
        WallClockBudget: TimeSpan.FromSeconds(3),
        CapturesScreenshot: false,
        ScreenshotFrameIndex: 0,
        SmokeName: "opus-alpha-smoke");

    /// <summary>Returns a copy that captures one screenshot at
    /// <paramref name="frameIndex"/>.</summary>
    public AlphaSmokeProfile WithScreenshot(int frameIndex) => this with
    {
        CapturesScreenshot = true,
        ScreenshotFrameIndex = frameIndex,
    };

    /// <summary>Throws when the profile is internally inconsistent. Smoke runs validate
    /// once at start so a bad profile fails fast instead of producing half-evidence.</summary>
    public void Validate()
    {
        if (FrameTarget < MinimumFrameTarget || FrameTarget > MaximumFrameTarget)
        {
            throw new ArgumentOutOfRangeException(
                nameof(FrameTarget),
                $"FrameTarget must be between {MinimumFrameTarget} and {MaximumFrameTarget}.");
        }

        if (FrameDelta <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(FrameDelta), "FrameDelta must be positive.");
        }

        if (WallClockBudget <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(WallClockBudget), "WallClockBudget must be positive.");
        }

        if (CapturesScreenshot
            && (ScreenshotFrameIndex < 0 || ScreenshotFrameIndex >= FrameTarget))
        {
            throw new ArgumentOutOfRangeException(
                nameof(ScreenshotFrameIndex),
                $"ScreenshotFrameIndex must be inside [0, {FrameTarget}) when CapturesScreenshot is true.");
        }

        if (string.IsNullOrWhiteSpace(SmokeName))
        {
            throw new ArgumentException("SmokeName must not be empty.", nameof(SmokeName));
        }
    }
}
