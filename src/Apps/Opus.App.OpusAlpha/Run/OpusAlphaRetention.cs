using Opus.Engine.Diagnostics.Reports;
using Opus.Foundation;

namespace Opus.App.OpusAlpha.Run;

/// <summary>
/// The diagnostics-evidence retention budget the alpha host applies to every artifact it
/// writes — rolling logs and paired failure / smoke / stress reports — so a tester machine
/// running the host repeatedly stays bounded instead of accumulating evidence forever.
/// <para>
/// One source of truth keeps the log and report budgets aligned (both default to 50 items
/// and 30 days). The engine-side retention machinery — the sweepers and the per-writer
/// retention options — shipped with M11.3; this is where the runnable host turns it on
/// instead of leaving every budget at the backward-compatible <c>Disabled</c> default.
/// </para>
/// </summary>
public static class OpusAlphaRetention
{
    /// <summary>Retention applied to paired failure / smoke / stress report artifacts.</summary>
    public static DiagnosticsArtifactRetentionPolicy Artifacts => DiagnosticsArtifactRetentionPolicy.Default;

    /// <summary>Retention applied to the rolling diagnostics log files.</summary>
    public static RollingLogRetentionPolicy Logs => RollingLogRetentionPolicy.Default;
}
