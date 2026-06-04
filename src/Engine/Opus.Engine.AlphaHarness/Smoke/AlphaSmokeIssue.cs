using System;

namespace Opus.Engine.AlphaHarness.Smoke;

/// <summary>
/// One issue recorded by the smoke runner. Captures the stable <c>OPDX-ALH-*</c> code,
/// the originating <see cref="AlphaSmokeIssueCode"/>, a one-line human message, and the
/// UTC timestamp at which the runner observed the condition.
/// </summary>
public sealed record AlphaSmokeIssue(
    AlphaSmokeIssueCode Code,
    string DiagnosticCode,
    string Message,
    DateTimeOffset ObservedAtUtc)
{
    /// <summary>Builds an issue from <paramref name="code"/> and a <paramref name="message"/>;
    /// the diagnostic code field is resolved through the canonical mapping so callers do
    /// not duplicate the lookup.</summary>
    public static AlphaSmokeIssue Create(
        AlphaSmokeIssueCode code,
        string message,
        DateTimeOffset observedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Issue message must not be empty.", nameof(message));
        }

        return new AlphaSmokeIssue(
            code,
            code.ToDiagnosticCode(),
            message,
            observedAtUtc.ToUniversalTime());
    }
}
