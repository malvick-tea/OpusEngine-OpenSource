using Opus.Foundation;

namespace Opus.Engine.AlphaHarness.Smoke;

/// <summary>Structured issue surfaced when the smoke report writer cannot persist a
/// paired JSON+TXT bundle. Mirrors <c>FailureReportWriteIssue</c> so reporters that
/// already understand the M7 contract get the same shape for M9 evidence.</summary>
public sealed record AlphaSmokeReportWriteIssue(
    string Code,
    LogLevel Severity,
    string Path,
    string Message,
    string RemediationHint);
