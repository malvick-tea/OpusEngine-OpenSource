using System;
using System.Collections.Generic;
using System.Linq;

namespace Opus.Engine.AlphaHarness.Smoke;

/// <summary>
/// Outcome of a single alpha-host smoke run. Captures the profile, wall-clock elapsed,
/// frame accounting, optional screenshot path, and every observed issue. The
/// <see cref="IsClean"/> shape mirrors <c>NetSoakReport.IsClean</c> so reporters that
/// already understand the M8 contract treat the smoke the same way.
/// </summary>
public sealed record AlphaSmokeOutcome(
    AlphaSmokeProfile Profile,
    DateTimeOffset StartedAtUtc,
    TimeSpan ElapsedWallClock,
    int FramesStepped,
    TimeSpan MeanCpuFrameTime,
    TimeSpan MaxCpuFrameTime,
    TimeSpan P95CpuFrameTime,
    string? ScreenshotPath,
    IReadOnlyList<AlphaSmokeIssue> Issues)
{
    /// <summary>True when no issues were recorded; alpha-quality runs treat this as the
    /// pass/fail gate for a smoke check.</summary>
    public bool IsClean => Issues.Count == 0;

    /// <summary>True when the runner observed every frame the profile asked for.</summary>
    public bool ReachedFrameTarget => FramesStepped >= Profile.FrameTarget;

    /// <summary>Returns issues with the supplied code; never null.</summary>
    public IEnumerable<AlphaSmokeIssue> IssuesWithCode(AlphaSmokeIssueCode code) =>
        Issues.Where(issue => issue.Code == code);

    /// <summary>Convenience builder used by the smoke runner; centralises argument
    /// validation so the smoke pipeline never produces a half-shaped outcome.</summary>
    public static AlphaSmokeOutcome Create(
        AlphaSmokeProfile profile,
        DateTimeOffset startedAtUtc,
        TimeSpan elapsedWallClock,
        int framesStepped,
        TimeSpan meanCpuFrameTime,
        TimeSpan maxCpuFrameTime,
        TimeSpan p95CpuFrameTime,
        string? screenshotPath,
        IEnumerable<AlphaSmokeIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(issues);
        if (framesStepped < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(framesStepped), "framesStepped must be non-negative.");
        }

        if (elapsedWallClock < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedWallClock), "elapsedWallClock must be non-negative.");
        }

        var sortedIssues = issues
            .Where(static issue => issue is not null)
            .OrderBy(static issue => issue!.ObservedAtUtc)
            .ThenBy(static issue => issue!.DiagnosticCode, StringComparer.Ordinal)
            .ToArray();

        return new AlphaSmokeOutcome(
            profile,
            startedAtUtc.ToUniversalTime(),
            elapsedWallClock,
            framesStepped,
            meanCpuFrameTime,
            maxCpuFrameTime,
            p95CpuFrameTime,
            NormaliseScreenshotPath(screenshotPath),
            sortedIssues);
    }

    private static string? NormaliseScreenshotPath(string? screenshotPath)
    {
        if (screenshotPath is null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(screenshotPath) ? null : screenshotPath;
    }
}
