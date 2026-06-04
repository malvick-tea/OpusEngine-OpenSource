namespace Opus.Engine.AlphaHarness.Machine;

/// <summary>One difference observed between a reference <see cref="KnownGoodMachineProfile"/>
/// and a fresh capture. Carries the stable <c>OPDX-ALH-2xx</c> diagnostic code, severity,
/// the field name, the expected/actual values, and a one-line message suitable for log
/// output.</summary>
public sealed record MachineProfileDifference(
    string DiagnosticCode,
    MachineProfileDifferenceSeverity Severity,
    string Field,
    string ExpectedValue,
    string ActualValue,
    string Message);

/// <summary>Severity attached to one machine-profile difference.</summary>
public enum MachineProfileDifferenceSeverity
{
    /// <summary>Informational — field matches the reference.</summary>
    Info,

    /// <summary>Warning — field differs but is not a hard match requirement.</summary>
    Warning,

    /// <summary>Error — field difference is a known compatibility blocker.</summary>
    Error,
}
