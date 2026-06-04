namespace Opus.Engine.AlphaHarness.Packaging;

/// <summary>Severity attached to a single <see cref="AlphaPackageChecklistFinding"/>.
/// Mirrors the <c>PackageDiagnosticSeverity</c> ladder so reporters can fold both
/// surfaces into one tester table.</summary>
public enum AlphaPackageChecklistSeverity
{
    /// <summary>Informational — feature is present and satisfies the checklist.</summary>
    Info,

    /// <summary>Warning — checklist is not blocked but a recommended item is missing
    /// (currently unused in the default policy; reserved for future tightening).</summary>
    Warning,

    /// <summary>Error — checklist item is required for alpha and is missing.</summary>
    Error,
}
