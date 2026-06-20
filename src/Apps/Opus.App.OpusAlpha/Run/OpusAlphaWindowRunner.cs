using System;
using System.Diagnostics;
using System.Linq;
using Opus.App.OpusAlpha.Cli;
using Opus.Engine.Consumer.Integration;
using Opus.Engine.Diagnostics.Overlay;
using Opus.Engine.Diagnostics.Reports;
using Opus.Engine.Host.Windows.Direct3D12;
using Opus.Engine.Host.Windows.Direct3D12.Diagnostics;
using Opus.Engine.Host.Windows.Direct3D12.Frame;
using Opus.Engine.Runtime;
using Opus.Foundation;

namespace Opus.App.OpusAlpha.Run;

/// <summary>
/// Runs the live-window mode of the alpha host (Mode = Window). Preserves the M5.1
/// behaviour: open the D3D12 window, drive variable-delta frames until Ctrl-C or the
/// window closes, dispose cleanly. Tester evidence (rolling log, failure report) wired
/// identically to the legacy <c>Program.Main</c> path that lived here before M9.
/// </summary>
public static class OpusAlphaWindowRunner
{
    private static readonly TimeSpan TargetFrameDelta = TimeSpan.FromMilliseconds(16.7);

    /// <summary>Runs the live window; returns the conventional process exit code.</summary>
    public static int Run(OpusAlphaArgs args, ILog consoleLog, ConsumerIntegration? consumerIntegration = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(consoleLog);
        var effectiveArgs = ApplyTesterSettings(args, consoleLog);
        var options = BuildOptions(effectiveArgs, consumerIntegration);
        var rollingLog = OpusAlphaLogging.TryCreateRollingLog(options, consoleLog, effectiveArgs.EnableAsyncLogging);
        using var compositeLog = rollingLog is null ? null : CompositeLog.Create(consoleLog, rollingLog);
        var log = (ILog?)compositeLog ?? consoleLog;
        var reportWriter = new FailureReportWriter(
            BuildReportWriterOptions(OpusDiagnosticsPaths.ReportsDirectory(options.EffectiveDiagnosticsDirectory)));

        D3D12OpusHostInstance? instance = null;
        try
        {
            AlphaFailureInjection.ThrowIfRequested(effectiveArgs.InjectFailure, log);
            instance = new D3D12OpusHostBuilder()
                .WithLog(log)
                .WithOptions(options)
                .TryBuild();
            if (instance is null)
            {
                log.Warn("D3D12 host could not be opened: no compatible adapter, no SDL video, or non-Windows OS.");
                WriteFailureReport(reportWriter, rollingLog, options, null, FailureReportKind.StartupFailure, null, log);
                return 1;
            }

            RunHost(instance);
            return 0;
        }
        catch (Exception ex)
        {
            var kind = ClassifyFailure(instance, ex);
            WriteFailureReport(reportWriter, rollingLog, options, instance, kind, ex, log);
            log.Critical("Opus alpha host stopped after an unhandled failure.", ex);
            return 2;
        }
        finally
        {
            instance?.Dispose();
        }
    }

    /// <summary>Builds the failure-report writer options with the alpha host's retention
    /// budget applied, so repeated tester runs do not accumulate report bundles forever.</summary>
    public static FailureReportWriterOptions BuildReportWriterOptions(string reportsDirectory) =>
        new(reportsDirectory, OpusAlphaRetention.Artifacts);

    /// <summary>Maps parsed CLI args plus an optional consumer integration into the live
    /// host options. Pure and deterministic so the option wiring (scene / asset /
    /// diagnostics dir / frame-budget watchdog) is unit-tested without opening a window.</summary>
    public static D3D12OpusApplicationOptions BuildOptions(
        OpusAlphaArgs args,
        ConsumerIntegration? consumerIntegration)
    {
        ArgumentNullException.ThrowIfNull(args);
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

        if (args.EnableFrameBudget)
        {
            options = options with { FrameBudget = D3D12AlphaFrameBudgetPolicy.Enable() };
        }

        if (args.OverlayLevel is { } overlayLevel)
        {
            options = options with
            {
                DiagnosticOverlay = DiagnosticOverlayOptions.Default with { Level = overlayLevel },
            };
        }

        return options;
    }

    /// <summary>Overlays a persisted tester-settings file (when <c>--settings</c> is supplied) onto
    /// the parsed args, so the file is the source of truth for the window knobs it carries (scene /
    /// overlay / frame-budget / async-logging). A missing file is seeded with defaults; a corrupt
    /// one falls back to defaults without aborting the launch. Returns the args unchanged when no
    /// settings path was given. Public so the settings overlay is unit-tested without opening a
    /// window, mirroring <see cref="BuildOptions"/>.</summary>
    public static OpusAlphaArgs ApplyTesterSettings(OpusAlphaArgs args, ILog log)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(log);
        if (string.IsNullOrWhiteSpace(args.SettingsPath))
        {
            return args;
        }

        var settings = TesterSettingsStore.LoadOrCreate(args.SettingsPath, log);
        return args with
        {
            SceneScale = settings.SceneScale,
            OverlayLevel = settings.OverlayLevel,
            EnableFrameBudget = settings.EnableFrameBudget,
            EnableAsyncLogging = settings.EnableAsyncLogging,
        };
    }

    private static void RunHost(D3D12OpusHostInstance instance)
    {
        void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs eventArgs)
        {
            eventArgs.Cancel = true;
            instance.Host.RequestShutdown();
        }

        Console.CancelKeyPress += OnCancelKeyPress;
        try
        {
            instance.Host.Start();
            var clock = Stopwatch.StartNew();
            var lastElapsed = clock.Elapsed;
            while (true)
            {
                var now = clock.Elapsed;
                var delta = now - lastElapsed;
                lastElapsed = now;
                if (!instance.Host.Step(delta == TimeSpan.Zero ? TargetFrameDelta : delta))
                {
                    break;
                }
            }
        }
        finally
        {
            Console.CancelKeyPress -= OnCancelKeyPress;
        }
    }

    private static FailureReportKind ClassifyFailure(
        D3D12OpusHostInstance? instance,
        Exception exception)
    {
        // The engine throws typed failures (EngineDeviceLostException /
        // EngineContentException) at its device and content boundaries; trust the classifier
        // whenever it recognises one. This replaces the old coarse heuristic that guessed
        // ContentFailure purely from whether an asset path was configured.
        var classified = FailureReportClassifier.Classify(exception);
        if (classified != FailureReportKind.Crash)
        {
            return classified;
        }

        // No recognised engine failure type: if the host never finished building, the
        // failure happened during startup; otherwise it is an unclassified in-run crash.
        return instance is null ? FailureReportKind.StartupFailure : FailureReportKind.Crash;
    }

    private static void WriteFailureReport(
        FailureReportWriter writer,
        IRollingLogSink? rollingLog,
        D3D12OpusApplicationOptions options,
        D3D12OpusHostInstance? instance,
        FailureReportKind kind,
        Exception? exception,
        ILog log)
    {
        var report = FailureReport.Capture(
            kind,
            DateTimeOffset.UtcNow,
            BuildInfo.Current,
            D3D12DiagnosticSnapshots.ToFailureReportAdapter(instance),
            OpusAlphaLogging.SnapshotLogLines(rollingLog),
            instance?.Application.LastScreenshotPath,
            exception,
            D3D12NetTelemetryAdapter.ToFailureReportNetworkSnapshot(
                options.NetTelemetryProvider?.Invoke()),
            consumerLines: instance?.Application.CaptureConsumerFailureReportLines());
        var result = writer.Write(report);
        if (result.Succeeded)
        {
            log.Warn($"Failure report written: {result.TextPath}");
            if (result.AttachedScreenshotPath is { } attachedScreenshot)
            {
                log.Info($"Failure report screenshot attached: {attachedScreenshot}");
            }

            return;
        }

        var issue = result.Issue;
        log.Error(issue is null
            ? "Failure report write failed without a structured issue."
            : $"{issue.Code}: {issue.Message} Remediation: {issue.RemediationHint}");
    }
}
