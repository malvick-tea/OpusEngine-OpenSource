using System;

namespace Opus.Engine.AlphaStress.Stress;

/// <summary>
/// Maps <see cref="AlphaStressIssueCode"/> values to stable <c>OPDX-STR-*</c>
/// diagnostic codes. Centralised so report writers, log sinks, and tests share one
/// source of truth and a new code can never be added without the diagnostic surface
/// learning about it.
/// </summary>
public static class AlphaStressIssueCodeStrings
{
    /// <summary>Returns the stable diagnostic code for <paramref name="code"/>.
    /// Throws when the enum is extended without updating the mapping — this is loud by
    /// design so a future contributor cannot ship a silently-untranslated value.</summary>
    public static string ToDiagnosticCode(AlphaStressIssueCode code) => code switch
    {
        AlphaStressIssueCode.HostUnavailable => AlphaStressDiagnosticCodes.StressHostUnavailable,
        AlphaStressIssueCode.BudgetExceeded => AlphaStressDiagnosticCodes.StressBudgetExceeded,
        AlphaStressIssueCode.IterationFailed => AlphaStressDiagnosticCodes.StressIterationFailed,
        AlphaStressIssueCode.FramePacingDegraded => AlphaStressDiagnosticCodes.StressFramePacingDegraded,
        AlphaStressIssueCode.MemoryGrowthExceeded => AlphaStressDiagnosticCodes.StressMemoryGrowthExceeded,
        AlphaStressIssueCode.FaultInjectionDegraded => AlphaStressDiagnosticCodes.StressFaultInjectionDegraded,
        AlphaStressIssueCode.IterationUnhandledException => AlphaStressDiagnosticCodes.StressIterationUnhandledException,
        AlphaStressIssueCode.KnownIssueBlockerOpen => AlphaStressDiagnosticCodes.KnownIssueBlockerOpen,
        AlphaStressIssueCode.KnownIssueMustFixOpen => AlphaStressDiagnosticCodes.KnownIssueMustFixOpen,
        _ => throw new ArgumentOutOfRangeException(
            nameof(code),
            $"AlphaStressIssueCode '{code}' has no diagnostic-code mapping; extend AlphaStressIssueCodeStrings before shipping."),
    };
}
