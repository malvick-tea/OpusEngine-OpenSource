using System;

namespace Opus.Engine.Diagnostics.Reports;

/// <summary>Filesystem configuration for <see cref="FailureReportWriter"/>.</summary>
/// <param name="DirectoryPath">Directory the writer persists paired failure-report
/// artifacts into.</param>
/// <param name="Retention">Optional retention policy applied before each write so a
/// tester machine does not accumulate failure-report evidence indefinitely. Null falls
/// back to <see cref="DiagnosticsArtifactRetentionPolicy.Disabled"/> for backward
/// compatibility with existing M7 callers.</param>
public sealed record FailureReportWriterOptions(
    string DirectoryPath,
    DiagnosticsArtifactRetentionPolicy? Retention = null)
{
    /// <summary>Stem prefix used by every Opus failure report; the retention sweeper
    /// matches against this when scanning the configured directory.</summary>
    public const string ArtifactStemPrefix = "opus-";

    /// <summary>Creates options targeting the standard Opus diagnostics report directory
    /// with retention disabled.</summary>
    public static FailureReportWriterOptions Default { get; } = new(
        OpusDiagnosticsPaths.DefaultReportsDirectory(),
        Retention: null);

    /// <summary>Returns the active retention policy. Null falls back to
    /// <see cref="DiagnosticsArtifactRetentionPolicy.Disabled"/>.</summary>
    public DiagnosticsArtifactRetentionPolicy EffectiveRetention =>
        Retention ?? DiagnosticsArtifactRetentionPolicy.Disabled;

    /// <summary>Validates writer options before report output.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DirectoryPath))
        {
            throw new ArgumentException("Failure report directory must not be empty.", nameof(DirectoryPath));
        }

        EffectiveRetention.Validate();
    }
}
