using Opus.Foundation;

namespace Opus.Engine.AlphaStress.Stress;

/// <summary>
/// Structured issue returned by <see cref="AlphaStressReportWriter"/> when the paired
/// JSON+TXT stress report cannot be persisted. Mirrors the M9 smoke-writer issue shape
/// so reporters that already understand the alpha-harness writers do not need a new
/// integration.
/// </summary>
/// <param name="Code">Stable diagnostic code (<c>OPDX-STR-007</c>).</param>
/// <param name="Severity">Log level the host should emit the issue at.</param>
/// <param name="Path">Directory path the write attempted to reach.</param>
/// <param name="Message">Invariant message describing the failure.</param>
/// <param name="RemediationHint">Operator-facing remediation hint.</param>
public sealed record AlphaStressReportWriteIssue(
    string Code,
    LogLevel Severity,
    string Path,
    string Message,
    string RemediationHint);
