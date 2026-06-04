using System;
using Opus.Engine.Diagnostics.Reports;

namespace Opus.Engine.AlphaHarness.Smoke;

/// <summary>Filesystem configuration for <see cref="AlphaSmokeReportWriter"/>. The
/// directory layout reuses <see cref="OpusDiagnosticsPaths"/> so smoke evidence lands
/// inside the same tester-evidence root as M7 failure reports and rolling logs.</summary>
/// <param name="DirectoryPath">Directory the writer persists paired smoke-report
/// artifacts into.</param>
/// <param name="Retention">Optional retention policy applied before each write so a
/// tester machine does not accumulate smoke evidence indefinitely. Null falls back to
/// <see cref="DiagnosticsArtifactRetentionPolicy.Disabled"/> for backward compatibility
/// with the M9 callers that did not specify retention.</param>
public sealed record AlphaSmokeReportWriterOptions(
    string DirectoryPath,
    DiagnosticsArtifactRetentionPolicy? Retention = null)
{
    /// <summary>Stem prefix used by every alpha smoke report; the retention sweeper
    /// matches against this when scanning the configured directory.</summary>
    public const string ArtifactStemPrefix = "opus-alpha-smoke-";

    /// <summary>Returns options targeting the default Opus diagnostics smoke directory.</summary>
    public static AlphaSmokeReportWriterOptions Default { get; } = new(
        OpusDiagnosticsPaths.SmokeDirectory(OpusDiagnosticsPaths.DefaultRootDirectory()),
        Retention: null);

    /// <summary>Returns the active retention policy. Null falls back to
    /// <see cref="DiagnosticsArtifactRetentionPolicy.Disabled"/>.</summary>
    public DiagnosticsArtifactRetentionPolicy EffectiveRetention =>
        Retention ?? DiagnosticsArtifactRetentionPolicy.Disabled;

    /// <summary>Validates options before any write opens a file handle.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DirectoryPath))
        {
            throw new ArgumentException("Smoke report directory must not be empty.", nameof(DirectoryPath));
        }

        EffectiveRetention.Validate();
    }
}
