namespace Opus.Engine.Diagnostics.Reports;

/// <summary>Top-level failure category captured for tester evidence packages.</summary>
public enum FailureReportKind
{
    StartupFailure = 0,
    ContentFailure = 1,
    DeviceLost = 2,
    Crash = 3,
}
