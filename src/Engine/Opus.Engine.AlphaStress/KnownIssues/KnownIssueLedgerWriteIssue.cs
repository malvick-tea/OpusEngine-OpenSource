using Opus.Foundation;

namespace Opus.Engine.AlphaStress.KnownIssues;

/// <summary>
/// Structured issue returned by <see cref="KnownIssueLedgerWriter"/> when the JSON
/// ledger could not be persisted. Mirrors the M9 smoke-report-write issue shape so
/// reporters that already understand the alpha-harness writers do not need a new
/// integration.
/// </summary>
/// <param name="Code">Stable diagnostic code (<c>OPDX-STR-101</c>).</param>
/// <param name="Severity">Log level the host should emit the issue at.</param>
/// <param name="Path">Filesystem path the write attempted to reach.</param>
/// <param name="Message">Invariant message describing the failure (exception type +
/// message). Suitable for log lines.</param>
/// <param name="RemediationHint">Operator-facing remediation hint.</param>
public sealed record KnownIssueLedgerWriteIssue(
    string Code,
    LogLevel Severity,
    string Path,
    string Message,
    string RemediationHint);
