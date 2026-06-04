namespace Opus.Engine.AlphaHarness.Smoke;

/// <summary>
/// Issue kinds the smoke runner can record into <see cref="AlphaSmokeOutcome.Issues"/>.
/// Each value maps to one stable <see cref="AlphaHarnessDiagnosticCodes"/> string so
/// reporters do not need to switch on enum names.
/// </summary>
public enum AlphaSmokeIssueCode
{
    /// <summary>Host could not be built (no D3D12 adapter / SDL video / non-Windows).</summary>
    HostUnavailable,

    /// <summary>Wall-clock budget elapsed before the frame target was reached.</summary>
    BudgetExceeded,

    /// <summary>Host stopped (returned false from <c>Step</c>) before the frame target.</summary>
    HostStoppedEarly,

    /// <summary>Frame loop threw an unhandled exception.</summary>
    UnhandledException,

    /// <summary>Screenshot was requested but no PNG file exists on disk afterwards.</summary>
    ScreenshotMissing,
}

/// <summary>Mapping table from <see cref="AlphaSmokeIssueCode"/> to the stable
/// <c>OPDX-ALH-*</c> diagnostic string. Lives alongside the enum so callers always go
/// through one resolver — no scattered switch statements over enum names.</summary>
public static class AlphaSmokeIssueCodeStrings
{
    /// <summary>Returns the stable diagnostic string for <paramref name="code"/>.</summary>
    public static string ToDiagnosticCode(this AlphaSmokeIssueCode code) => code switch
    {
        AlphaSmokeIssueCode.HostUnavailable => AlphaHarnessDiagnosticCodes.SmokeHostUnavailable,
        AlphaSmokeIssueCode.BudgetExceeded => AlphaHarnessDiagnosticCodes.SmokeBudgetExceeded,
        AlphaSmokeIssueCode.HostStoppedEarly => AlphaHarnessDiagnosticCodes.SmokeHostStoppedEarly,
        AlphaSmokeIssueCode.UnhandledException => AlphaHarnessDiagnosticCodes.SmokeUnhandledException,
        AlphaSmokeIssueCode.ScreenshotMissing => AlphaHarnessDiagnosticCodes.SmokeScreenshotMissing,
        _ => "OPDX-ALH-000",
    };
}
