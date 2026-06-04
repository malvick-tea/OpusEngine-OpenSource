using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Opus.Engine.AlphaHarness.Smoke;
using Opus.Engine.AlphaStress.FramePacing;
using Opus.Engine.AlphaStress.Stress;
using Opus.Engine.Host.Windows.Direct3D12;
using Opus.Foundation;

namespace Opus.App.OpusAlpha.Run;

/// <summary>
/// D3D12-backed implementation of <see cref="IAlphaStressIterationRunner"/>. Each
/// iteration opens a fresh <see cref="D3D12OpusHostBuilder"/> instance, steps the
/// requested frame count, records a per-frame <see cref="FramePacingObservation"/>,
/// and disposes the host. Isolating iterations per instance lets the M11 memory probe
/// surface leaks that only appear after a host teardown cycle.
/// </summary>
public sealed class OpusAlphaStressIterationRunner : IAlphaStressIterationRunner
{
    private readonly D3D12OpusApplicationOptions _hostOptions;
    private readonly ILog _log;

    /// <summary>Creates the iteration runner with the supplied host options and log
    /// sink. The runner does not own the log; the caller is responsible for the sink's
    /// lifecycle.</summary>
    public OpusAlphaStressIterationRunner(D3D12OpusApplicationOptions hostOptions, ILog log)
    {
        ArgumentNullException.ThrowIfNull(hostOptions);
        ArgumentNullException.ThrowIfNull(log);
        _hostOptions = hostOptions;
        _log = log;
    }

    /// <inheritdoc />
    public AlphaStressIterationRunResult Run(int iterationIndex, AlphaSmokeProfile iterationProfile)
    {
        ArgumentNullException.ThrowIfNull(iterationProfile);
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        D3D12OpusHostInstance? instance = null;
        try
        {
            instance = new D3D12OpusHostBuilder().WithLog(_log).WithOptions(_hostOptions).TryBuild();
            if (instance is null)
            {
                stopwatch.Stop();
                return new AlphaStressIterationRunResult(
                    SmokeOutcome: null,
                    FramePacingObservations: Array.Empty<FramePacingObservation>(),
                    UnhandledException: null);
            }

            return StepIteration(iterationIndex, iterationProfile, instance, startedAt, stopwatch);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new AlphaStressIterationRunResult(
                SmokeOutcome: null,
                FramePacingObservations: Array.Empty<FramePacingObservation>(),
                UnhandledException: ex);
        }
        finally
        {
            instance?.Dispose();
        }
    }

    private static AlphaStressIterationRunResult StepIteration(
        int iterationIndex,
        AlphaSmokeProfile iterationProfile,
        D3D12OpusHostInstance instance,
        DateTimeOffset startedAt,
        Stopwatch stopwatch)
    {
        var observations = new List<FramePacingObservation>(iterationProfile.FrameTarget);
        var issues = new List<AlphaSmokeIssue>();
        instance.Host.Start();
        var stepped = 0;
        var frameStopwatch = new Stopwatch();
        for (var frame = 0; frame < iterationProfile.FrameTarget; frame++)
        {
            if (stopwatch.Elapsed > iterationProfile.WallClockBudget)
            {
                issues.Add(AlphaSmokeIssue.Create(
                    AlphaSmokeIssueCode.BudgetExceeded,
                    BudgetMessage(iterationIndex, iterationProfile, frame),
                    DateTimeOffset.UtcNow));
                break;
            }

            frameStopwatch.Restart();
            var alive = instance.Host.Step(iterationProfile.FrameDelta);
            frameStopwatch.Stop();
            stepped++;
            observations.Add(new FramePacingObservation(
                FrameNumber: stepped,
                ObservedAtUtc: DateTimeOffset.UtcNow,
                CpuFrameTime: frameStopwatch.Elapsed));
            if (alive)
            {
                continue;
            }

            issues.Add(AlphaSmokeIssue.Create(
                AlphaSmokeIssueCode.HostStoppedEarly,
                StoppedEarlyMessage(iterationIndex, iterationProfile, frame),
                DateTimeOffset.UtcNow));
            break;
        }

        instance.Host.Stop();
        stopwatch.Stop();
        var snapshot = instance.Application.Metrics.Snapshot();
        var outcome = AlphaSmokeOutcome.Create(
            iterationProfile,
            startedAt,
            stopwatch.Elapsed,
            framesStepped: stepped,
            meanCpuFrameTime: snapshot.Mean,
            maxCpuFrameTime: snapshot.Max,
            p95CpuFrameTime: snapshot.P95,
            screenshotPath: null,
            issues);
        return new AlphaStressIterationRunResult(
            SmokeOutcome: outcome,
            FramePacingObservations: observations,
            UnhandledException: null);
    }

    private static string BudgetMessage(int iterationIndex, AlphaSmokeProfile profile, int frame) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"iteration {iterationIndex}: wall-clock budget {profile.WallClockBudget.TotalMilliseconds:F0}ms elapsed at frame {frame}");

    private static string StoppedEarlyMessage(int iterationIndex, AlphaSmokeProfile profile, int frame) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"iteration {iterationIndex}: host stopped at frame {frame} before the {profile.FrameTarget}-frame target");
}
