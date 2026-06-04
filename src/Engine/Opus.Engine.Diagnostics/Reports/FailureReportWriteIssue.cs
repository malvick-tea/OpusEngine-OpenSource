using Opus.Foundation;

namespace Opus.Engine.Diagnostics.Reports;

/// <summary>Structured issue returned when a failure report cannot be written.</summary>
public sealed record FailureReportWriteIssue(
    string Code,
    LogLevel Severity,
    string Path,
    string Message,
    string RemediationHint);
