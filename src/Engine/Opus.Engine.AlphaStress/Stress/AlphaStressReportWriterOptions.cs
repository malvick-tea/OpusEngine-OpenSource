using System;
using Opus.Engine.Diagnostics.Reports;

namespace Opus.Engine.AlphaStress.Stress;

/// <summary>
/// Filesystem options for <see cref="AlphaStressReportWriter"/>. Mirrors the M9
/// smoke-writer options shape so the alpha-harness diagnostics root can host both
/// surfaces side-by-side under the same convention.
/// </summary>
/// <param name="DirectoryPath">Absolute or relative directory the writer persists the
/// paired stress report into. Created on demand.</param>
/// <param name="Retention">Optional retention policy applied before each write so a
/// tester machine does not accumulate stress evidence indefinitely. Null falls back to
/// <see cref="DiagnosticsArtifactRetentionPolicy.Disabled"/> for backward compatibility
/// with the M11 callers that did not specify retention.</param>
public sealed record AlphaStressReportWriterOptions(
    string DirectoryPath,
    DiagnosticsArtifactRetentionPolicy? Retention = null)
{
    /// <summary>Stem prefix used by every alpha stress report; the retention sweeper
    /// matches against this when scanning the configured directory.</summary>
    public const string ArtifactStemPrefix = "opus-alpha-stress-";

    /// <summary>Returns the active retention policy. Null falls back to
    /// <see cref="DiagnosticsArtifactRetentionPolicy.Disabled"/>.</summary>
    public DiagnosticsArtifactRetentionPolicy EffectiveRetention =>
        Retention ?? DiagnosticsArtifactRetentionPolicy.Disabled;

    /// <summary>Throws when the options are inconsistent.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DirectoryPath))
        {
            throw new ArgumentException("DirectoryPath must not be empty.", nameof(DirectoryPath));
        }

        EffectiveRetention.Validate();
    }
}
