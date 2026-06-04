using System;
using System.Linq;
using Opus.Engine.Diagnostics.Reports;
using Opus.Engine.Host.Windows.Direct3D12;
using Opus.Foundation;

namespace Opus.App.OpusAlpha.Run;

/// <summary>Shared logging helpers used across the M9 runners — rolling log lifecycle
/// and rolling-tail snapshotting. Pulled out of the legacy <c>Program</c> body so each
/// runner can reuse it without duplicating the prototype IO contract.</summary>
public static class OpusAlphaLogging
{
    /// <summary>Default number of rolling log lines attached to a failure-report payload.</summary>
    public const int FailureReportTailLineCount = FailureReport.DefaultLogLineCount;

    /// <summary>Opens the rolling diagnostics log inside the active diagnostics root.
    /// Returns null when the directory cannot be opened; the caller continues with the
    /// supplied <paramref name="fallbackLog"/> alone in that case. When
    /// <paramref name="useAsyncWrites"/> is true the file sink is wrapped in an off-thread
    /// <see cref="AsyncRollingLogSink"/> (the wrapper owns and disposes the inner sink) so
    /// disk IO no longer stalls the producer (game/render) thread.</summary>
    public static IRollingLogSink? TryCreateRollingLog(
        D3D12OpusApplicationOptions options,
        ILog fallbackLog,
        bool useAsyncWrites = false)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(fallbackLog);
        try
        {
            var logDirectory = OpusDiagnosticsPaths.LogsDirectory(options.EffectiveDiagnosticsDirectory);
            var fileSink = new RollingFileLogSink(
                RollingLogSinkOptions.ForDirectory(logDirectory) with { Retention = OpusAlphaRetention.Logs });
            return useAsyncWrites ? WrapAsync(fileSink) : fileSink;
        }
        catch (Exception ex)
        {
            fallbackLog.Warn("Rolling diagnostics log could not be opened; continuing with console logs only.");
            fallbackLog.Error("Rolling diagnostics log open failed.", ex);
            return null;
        }
    }

    private static IRollingLogSink WrapAsync(RollingFileLogSink fileSink)
    {
        try
        {
            return new AsyncRollingLogSink(fileSink, AsyncRollingLogSinkOptions.Default);
        }
        catch
        {
            // The wrapper could not start (e.g. the OS refused the worker thread): release
            // the file handle the inner sink already opened, then let the outer handler
            // fall back to console-only logging rather than leaking the handle.
            fileSink.Dispose();
            throw;
        }
    }

    /// <summary>Returns the most recent log entries the rolling sink kept in memory, or
    /// an empty array when the sink is null.</summary>
    public static string[] SnapshotLogLines(IRollingLogSink? rollingLog)
    {
        if (rollingLog is null)
        {
            return Array.Empty<string>();
        }

        return rollingLog
            .SnapshotTail(FailureReportTailLineCount)
            .Select(static entry => entry.ToDisplayLine())
            .ToArray();
    }
}
