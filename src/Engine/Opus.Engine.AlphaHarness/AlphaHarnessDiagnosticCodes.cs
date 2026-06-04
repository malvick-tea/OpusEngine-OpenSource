namespace Opus.Engine.AlphaHarness;

/// <summary>
/// Stable diagnostic codes emitted by the M9 alpha-host harness surface. Mirrors the
/// append-only convention already used by <c>OPDX-OVR-*</c>, <c>OPDX-REP-*</c>,
/// <c>OPDX-LOG-*</c>, and <c>OPDX-NET-*</c>: never renumber or repurpose a code once it has
/// shipped to a tester machine. New behaviour extends the namespace rather than shifting
/// existing values so log greps in the field stay reliable.
/// </summary>
public static class AlphaHarnessDiagnosticCodes
{
    // ---- Smoke (host run) -------------------------------------------------

    /// <summary>Smoke runner could not open the D3D12 host before stepping frames.</summary>
    public const string SmokeHostUnavailable = "OPDX-ALH-001";

    /// <summary>Smoke runner exceeded its frame budget without reaching the requested
    /// frame count.</summary>
    public const string SmokeBudgetExceeded = "OPDX-ALH-002";

    /// <summary>Smoke runner observed an unexpected host stop before the frame target.</summary>
    public const string SmokeHostStoppedEarly = "OPDX-ALH-003";

    /// <summary>Smoke runner caught an unhandled exception during frame stepping.</summary>
    public const string SmokeUnhandledException = "OPDX-ALH-004";

    /// <summary>Smoke screenshot was requested but the host did not produce a file.</summary>
    public const string SmokeScreenshotMissing = "OPDX-ALH-005";

    /// <summary>Smoke writer could not persist the paired JSON+TXT report.</summary>
    public const string SmokeReportWriteFailed = "OPDX-ALH-006";

    // ---- Package checklist ------------------------------------------------

    /// <summary>Required alpha-package feature flag missing from manifest.</summary>
    public const string PackageRequiredFeatureMissing = "OPDX-ALH-101";

    /// <summary>Required alpha-package asset kind missing from manifest entries.</summary>
    public const string PackageRequiredAssetKindMissing = "OPDX-ALH-102";

    /// <summary>Required alpha-package localisation locale missing.</summary>
    public const string PackageRequiredLocaleMissing = "OPDX-ALH-103";

    /// <summary>Manifest engine target does not match the current Opus product.</summary>
    public const string PackageEngineTargetMismatch = "OPDX-ALH-104";

    /// <summary>Underlying content validator surfaced one or more error diagnostics.</summary>
    public const string PackageUnderlyingValidationFailed = "OPDX-ALH-105";

    /// <summary>Manifest could not be loaded before the checklist could run.</summary>
    public const string PackageManifestUnavailable = "OPDX-ALH-106";

    // ---- Machine profile --------------------------------------------------

    /// <summary>Captured machine profile reports a different operating system family.</summary>
    public const string MachineOsFamilyMismatch = "OPDX-ALH-201";

    /// <summary>Captured machine profile reports a different processor architecture.</summary>
    public const string MachineArchitectureMismatch = "OPDX-ALH-202";

    /// <summary>Captured machine profile reports fewer logical processors than expected.</summary>
    public const string MachineProcessorCountBelowExpected = "OPDX-ALH-203";

    /// <summary>Captured machine profile reports a lower dotnet runtime version.</summary>
    public const string MachineDotnetRuntimeBelowExpected = "OPDX-ALH-204";

    /// <summary>Captured machine profile reports a different D3D12 adapter than expected.</summary>
    public const string MachineGraphicsAdapterMismatch = "OPDX-ALH-205";

    /// <summary>Captured machine profile reports an unknown graphics adapter (no D3D12).</summary>
    public const string MachineGraphicsAdapterUnknown = "OPDX-ALH-206";

    /// <summary>Reference known-good profile could not be loaded from the supplied path.</summary>
    public const string MachineReferenceUnavailable = "OPDX-ALH-207";
}
