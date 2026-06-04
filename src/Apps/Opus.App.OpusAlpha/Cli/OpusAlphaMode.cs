namespace Opus.App.OpusAlpha.Cli;

/// <summary>Top-level dispatch modes the M9 alpha host CLI supports. Each value maps to a
/// runner under <c>Runners/</c>. The default <see cref="Window"/> mode preserves the
/// M5.1 behaviour (open a live D3D12 window and drive frames until Ctrl-C).</summary>
public enum OpusAlphaMode
{
    /// <summary>Open a live D3D12 window and drive variable-delta frames until Ctrl-C.</summary>
    Window,

    /// <summary>Run a headless smoke: open the host, step a fixed frame target, optionally
    /// take a screenshot, write a paired JSON+TXT smoke report, then exit.</summary>
    Smoke,

    /// <summary>Validate an alpha content package against the M9 checklist and write a
    /// JSON report. Does not open the D3D12 host.</summary>
    ValidatePackage,

    /// <summary>Capture the current machine profile and either print or compare against a
    /// reference profile file.</summary>
    CheckMachine,

    /// <summary>Drive the M8 net soak harness through a loopback rig and print the
    /// resulting report. Does not open the D3D12 host.</summary>
    Soak,

    /// <summary>Drive the M11 alpha stress harness for the configured iteration count
    /// over the live D3D12 host, evaluate frame-pacing and memory thresholds, and write
    /// a paired JSON+TXT stress report under the diagnostics stress directory.</summary>
    Stress,

    /// <summary>Merge two known-issue ledgers into a single output ledger using the
    /// overlay-wins-on-id-collision policy. Does not open the D3D12 host.</summary>
    KnownIssuesMerge,

    /// <summary>Diff two known-issue ledgers and print or persist the structured
    /// change list. Does not open the D3D12 host.</summary>
    KnownIssuesDiff,

    /// <summary>Print usage banner and exit.</summary>
    Help,
}
