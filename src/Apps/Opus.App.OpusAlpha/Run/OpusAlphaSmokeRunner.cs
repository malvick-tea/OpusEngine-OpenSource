using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Opus.App.OpusAlpha.Cli;
using Opus.Engine.AlphaHarness.Smoke;
using Opus.Engine.Consumer.Integration;
using Opus.Engine.Diagnostics.Reports;
using Opus.Engine.Host.Windows.Direct3D12;
using Opus.Engine.Runtime;
using Opus.Foundation;

namespace Opus.App.OpusAlpha.Run;

/// <summary>
/// Headless smoke runner — opens the D3D12 host, steps the requested frame count,
/// optionally requests a screenshot, then writes a paired JSON+TXT smoke report. The
/// exit code is 0 on a clean run, 1 when the host cannot open at all, and 2 when the
/// smoke completed but recorded one or more issues.
/// </summary>
public static class OpusAlphaSmokeRunner
{
    private const int ExitClean = 0;
    private const int ExitHostUnavailable = 1;
    private const int ExitSmokeIssues = 2;

    /// <summary>Runs the headless smoke described by <paramref name="args"/>.</summary>
    public static int Run(OpusAlphaArgs args, ILog log, ConsumerIntegration? consumerIntegration = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        var profile = BuildProfile(args);
        try
        {
            profile.Validate();
        }
        catch (ArgumentException ex)
        {
            log.Error("Smoke profile is invalid; aborting.", ex);
            return ExitHostUnavailable;
        }

        var hostOptions = BuildHostOptions(args, consumerIntegration);
        var reportWriter = CreateReportWriter(args, hostOptions);
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var outcome = ExecuteSmoke(profile, hostOptions, log, startedAt, stopwatch);
        stopwatch.Stop();
        WriteOutcome(reportWriter, outcome, log);
        return outcome switch
        {
            { IsClean: true } => ExitClean,
            _ when outcome.IssuesWithCode(AlphaSmokeIssueCode.HostUnavailable).GetEnumerator().MoveNext() => ExitHostUnavailable,
            _ => ExitSmokeIssues,
        };
    }

    private static AlphaSmokeProfile BuildProfile(OpusAlphaArgs args)
    {
        var profile = AlphaSmokeProfile.Default with
        {
            FrameTarget = args.SmokeFrameCount,
        };
        if (args.SmokeScreenshotFrame is { } frame)
        {
            profile = profile.WithScreenshot(frame);
        }

        return profile;
    }

    private static D3D12OpusApplicationOptions BuildHostOptions(
        OpusAlphaArgs args,
        ConsumerIntegration? consumerIntegration)
    {
        var options = D3D12OpusApplicationOptions.Default with
        {
            SceneScale = args.SceneScale,
            ConsumerIntegration = consumerIntegration,
        };
        if (!string.IsNullOrWhiteSpace(args.AssetPath))
        {
            options = options with { AssetPath = args.AssetPath };
        }

        if (!string.IsNullOrWhiteSpace(args.DiagnosticsDirectory))
        {
            options = options with { DiagnosticsDirectory = args.DiagnosticsDirectory };
        }

        return options;
    }

    private static AlphaSmokeReportWriter CreateReportWriter(
        OpusAlphaArgs args,
        D3D12OpusApplicationOptions hostOptions)
    {
        var directory = string.IsNullOrWhiteSpace(args.SmokeReportPath)
            ? OpusDiagnosticsPaths.SmokeDirectory(hostOptions.EffectiveDiagnosticsDirectory)
            : args.SmokeReportPath;
        return new AlphaSmokeReportWriter(new AlphaSmokeReportWriterOptions(directory, OpusAlphaRetention.Artifacts));
    }

    private static AlphaSmokeOutcome ExecuteSmoke(
        AlphaSmokeProfile profile,
        D3D12OpusApplicationOptions hostOptions,
        ILog log,
        DateTimeOffset startedAt,
        Stopwatch stopwatch)
    {
        var issues = new List<AlphaSmokeIssue>();
        D3D12OpusHostInstance? instance = null;
        try
        {
            instance = new D3D12OpusHostBuilder().WithLog(log).WithOptions(hostOptions).TryBuild();
            if (instance is null)
            {
                issues.Add(AlphaSmokeIssue.Create(
                    AlphaSmokeIssueCode.HostUnavailable,
                    "D3D12 host could not be opened (no adapter / SDL video / non-Windows).",
                    DateTimeOffset.UtcNow));
                return AlphaSmokeOutcome.Create(
                    profile,
                    startedAt,
                    stopwatch.Elapsed,
                    framesStepped: 0,
                    TimeSpan.Zero,
                    TimeSpan.Zero,
                    TimeSpan.Zero,
                    screenshotPath: null,
                    issues);
            }

            return StepFrames(profile, instance, startedAt, stopwatch, issues);
        }
        catch (Exception ex)
        {
            issues.Add(AlphaSmokeIssue.Create(
                AlphaSmokeIssueCode.UnhandledException,
                $"{ex.GetType().Name}: {ex.Message}",
                DateTimeOffset.UtcNow));
            return AlphaSmokeOutcome.Create(
                profile,
                startedAt,
                stopwatch.Elapsed,
                ClampFrameCount(instance?.Application.Metrics.TotalFramesObserved ?? 0),
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero,
                instance?.Application.LastScreenshotPath,
                issues);
        }
        finally
        {
            instance?.Dispose();
        }
    }

    private static AlphaSmokeOutcome StepFrames(
        AlphaSmokeProfile profile,
        D3D12OpusHostInstance instance,
        DateTimeOffset startedAt,
        Stopwatch stopwatch,
        List<AlphaSmokeIssue> issues)
    {
        string? screenshotPath = null;
        if (profile.CapturesScreenshot)
        {
            screenshotPath = Path.Combine(
                Path.GetTempPath(),
                string.Create(CultureInfo.InvariantCulture, $"opus-smoke-{Guid.NewGuid():N}.png"));
        }

        instance.Host.Start();
        var stepped = 0;
        for (var frame = 0; frame < profile.FrameTarget; frame++)
        {
            if (stopwatch.Elapsed > profile.WallClockBudget)
            {
                issues.Add(AlphaSmokeIssue.Create(
                    AlphaSmokeIssueCode.BudgetExceeded,
                    $"Wall-clock budget {profile.WallClockBudget.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture)}ms elapsed at frame {frame}.",
                    DateTimeOffset.UtcNow));
                break;
            }

            if (profile.CapturesScreenshot && frame == profile.ScreenshotFrameIndex && screenshotPath is not null)
            {
                instance.Application.RequestScreenshot(screenshotPath);
            }

            var alive = instance.Host.Step(profile.FrameDelta);
            stepped++;
            if (!alive)
            {
                issues.Add(AlphaSmokeIssue.Create(
                    AlphaSmokeIssueCode.HostStoppedEarly,
                    $"Host stopped at frame {frame} before the {profile.FrameTarget}-frame target.",
                    DateTimeOffset.UtcNow));
                break;
            }
        }

        instance.Host.Stop();
        var snapshot = instance.Application.Metrics.Snapshot();
        var actualScreenshot = ResolveScreenshotPath(profile, screenshotPath, issues);
        return AlphaSmokeOutcome.Create(
            profile,
            startedAt,
            stopwatch.Elapsed,
            stepped,
            snapshot.Mean,
            snapshot.Max,
            snapshot.P95,
            actualScreenshot,
            issues);
    }

    private static string? ResolveScreenshotPath(
        AlphaSmokeProfile profile,
        string? screenshotPath,
        List<AlphaSmokeIssue> issues)
    {
        if (!profile.CapturesScreenshot || screenshotPath is null)
        {
            return null;
        }

        if (File.Exists(screenshotPath))
        {
            return screenshotPath;
        }

        issues.Add(AlphaSmokeIssue.Create(
            AlphaSmokeIssueCode.ScreenshotMissing,
            $"Expected screenshot '{screenshotPath}' was not produced by the host.",
            DateTimeOffset.UtcNow));
        return null;
    }

    private static int ClampFrameCount(long observed) => observed > int.MaxValue ? int.MaxValue : (int)observed;

    private static void WriteOutcome(AlphaSmokeReportWriter writer, AlphaSmokeOutcome outcome, ILog log)
    {
        var result = writer.Write(outcome);
        if (result.Succeeded)
        {
            log.Info($"Smoke report written: {result.TextPath}");
            return;
        }

        var issue = result.Issue;
        log.Error(issue is null
            ? "Smoke report write failed without a structured issue."
            : $"{issue.Code}: {issue.Message} Remediation: {issue.RemediationHint}");
    }
}
