namespace Opus.Engine.AlphaHarness.Packaging;

/// <summary>One checklist row produced by <see cref="AlphaPackageChecklist"/>. Carries the
/// stable <c>OPDX-ALH-1xx</c> diagnostic code, the descriptive item label that surfaced
/// the finding (e.g. <c>required-feature:models</c>), a human-readable message, and
/// severity.</summary>
public sealed record AlphaPackageChecklistFinding(
    string DiagnosticCode,
    AlphaPackageChecklistSeverity Severity,
    string Item,
    string Message);
