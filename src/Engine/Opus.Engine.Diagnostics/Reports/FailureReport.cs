using System;
using System.Collections.Generic;
using System.Linq;
using Opus.Foundation;

namespace Opus.Engine.Diagnostics.Reports;

/// <summary>Immutable tester failure report payload before it is written to disk.</summary>
public sealed record FailureReport(
    FailureReportKind Kind,
    DateTimeOffset CapturedAtUtc,
    BuildInfo Build,
    FailureReportAdapterSnapshot Adapter,
    IReadOnlyList<string> LastLogLines,
    string? ScreenshotPath,
    IReadOnlyList<FailureReportExceptionInfo> ExceptionChain,
    FailureReportNetworkSnapshot? Network = null)
{
    /// <summary>Default number of rolling log lines attached to a report.</summary>
    public const int DefaultLogLineCount = 80;

    /// <summary>Optional consumer-supplied evidence lines folded into the report by the
    /// host at failure time. Defaults to empty and is never null.</summary>
    public IReadOnlyList<string> ConsumerLines { get; init; } = Array.Empty<string>();

    /// <summary>Creates a report from the current host evidence snapshot. The adapter
    /// snapshot is mandatory — callers that have no live adapter pass
    /// <see cref="FailureReportAdapterSnapshot.Unavailable"/> explicitly so the report
    /// always carries an unambiguous shape (no nullable-as-sentinel inside the model).
    /// The network snapshot is optional: hosts without a wired network session pass
    /// <c>null</c> so the report shape stays compact when no peer state exists.</summary>
    public static FailureReport Capture(
        FailureReportKind kind,
        DateTimeOffset capturedAtUtc,
        BuildInfo build,
        FailureReportAdapterSnapshot adapter,
        IEnumerable<string> lastLogLines,
        string? screenshotPath,
        Exception? exception,
        FailureReportNetworkSnapshot? network = null,
        IEnumerable<string>? consumerLines = null)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(lastLogLines);
        var normalizedLogs = lastLogLines.Where(static line => line is not null).ToArray();
        var normalizedScreenshot = NormaliseScreenshotPath(screenshotPath);
        var normalizedConsumerLines = consumerLines is null
            ? Array.Empty<string>()
            : consumerLines.Where(static line => line is not null).ToArray();
        return new FailureReport(
            kind,
            capturedAtUtc.ToUniversalTime(),
            build,
            adapter,
            normalizedLogs,
            normalizedScreenshot,
            FailureReportExceptionInfo.From(exception),
            Network: network)
        {
            ConsumerLines = normalizedConsumerLines,
        };
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
