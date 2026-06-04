namespace Opus.Engine.Diagnostics;

/// <summary>
/// Stable diagnostic codes emitted by Opus diagnostics infrastructure. Codes are
/// append-only: never renumber or repurpose an entry once it has shipped to a tester
/// machine. The <c>OPDX-REP-*</c> namespace covers tester failure-report write paths;
/// <c>OPDX-OVR-*</c> covers overlay configuration and runtime issues; <c>OPDX-LOG-*</c>
/// covers rolling log sink edges that surface to lead diagnostics. Add new codes by
/// extending the relevant namespace rather than shifting existing values.
/// </summary>
public static class DiagnosticCodes
{
    /// <summary>Failure report writer could not write one or more output files.</summary>
    public const string FailureReportWriteFailed = "OPDX-REP-001";

    /// <summary>Failure report writer options are invalid.</summary>
    public const string FailureReportConfigurationInvalid = "OPDX-REP-002";

    /// <summary>Failure report writer could not access the diagnostics directory.</summary>
    public const string FailureReportDirectoryUnavailable = "OPDX-REP-003";

    /// <summary>Failure report writer was denied permission to create the report file.</summary>
    public const string FailureReportPermissionDenied = "OPDX-REP-004";

    /// <summary>Diagnostic overlay options are invalid.</summary>
    public const string OverlayConfigurationInvalid = "OPDX-OVR-001";
}
