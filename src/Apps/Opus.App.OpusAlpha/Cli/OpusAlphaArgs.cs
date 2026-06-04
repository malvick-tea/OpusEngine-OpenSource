using System;
using Opus.Engine.AlphaHarness.Scenes;
using Opus.Engine.Diagnostics.Overlay;

namespace Opus.App.OpusAlpha.Cli;

/// <summary>
/// Parsed command-line arguments for the M9 alpha host. The shape is data only —
/// behaviour lives in the per-mode runners. Validation happens at the parser boundary
/// (see <see cref="OpusAlphaCliParser"/>); the runners trust the shape.
/// </summary>
/// <param name="Mode">Top-level dispatch mode selected by the caller.</param>
/// <param name="SceneScale">Scene-density preset passed to the D3D12 host for window /
/// smoke modes.</param>
/// <param name="AssetPath">Optional glTF/GLB asset path for window / smoke modes.</param>
/// <param name="DiagnosticsDirectory">Optional override of the diagnostics root directory
/// (logs / reports / smoke evidence land here).</param>
/// <param name="InjectFailure">Deliberate failure family injected at host startup so a tester can
/// validate the diagnostics bundle is produced (Mode = Window). Default
/// <see cref="AlphaFaultKind.None"/>.</param>
/// <param name="EnableFrameBudget">When true (Mode = Window) the host runs the rolling
/// frame-budget watchdog and logs a structured warning each time the frame window breaches
/// the alpha pacing thresholds. Default false.</param>
/// <param name="EnableAsyncLogging">When true (Mode = Window) the rolling diagnostics log is
/// wrapped in an off-thread <c>AsyncRollingLogSink</c> so disk IO no longer stalls the
/// game/render thread. Default false (synchronous file sink) until tester throughput data
/// justifies making it the host default.</param>
/// <param name="OverlayLevel">Optional diagnostic-overlay verbosity for the live window
/// (Mode = Window). Null uses the host default (Full); <see cref="DiagnosticOverlayLevel.Off"/>
/// hides the overlay for clean screenshots.</param>
/// <param name="SettingsPath">Optional path to a persisted tester-settings JSON file (Mode =
/// Window). When set, the window runner loads scene scale / overlay level / frame-budget /
/// async-logging from it (seeding the file with defaults when it does not yet exist) so a tester's
/// preferred window configuration survives across runs instead of being retyped as flags.</param>
/// <param name="ConsumerAssemblyPath">Optional path to an external assembly exposing an
/// <see cref="Opus.Engine.Consumer.Integration.IConsumerIntegrationFactory"/> (Mode = Window or
/// Smoke). When set, the host loads that assembly in an isolating context and drives the consumer's
/// integration; null runs the built-in procedural sample scene.</param>
/// <param name="SmokeFrameCount">Frames the smoke runner steps before exiting.</param>
/// <param name="SmokeScreenshotFrame">Optional frame index at which the smoke runner
/// requests a screenshot. Null disables screenshot evidence for the run.</param>
/// <param name="SmokeReportPath">Optional explicit path where the smoke report is
/// written. Null routes evidence to the default Opus smoke directory.</param>
/// <param name="PackagePath">Path to the alpha package directory or manifest under
/// validation (Mode = ValidatePackage).</param>
/// <param name="MachineReferencePath">Optional path to a known-good machine profile JSON
/// (Mode = CheckMachine). When null the runner only captures and prints the current
/// profile.</param>
/// <param name="MachineSavePath">Optional path the check-machine runner serialises the
/// captured profile to. Null disables persistence so testers can do a print-only check.</param>
/// <param name="SoakPeers">Peer count for the loopback soak (Mode = Soak).</param>
/// <param name="SoakPacketsPerPeer">Packets-per-peer count for the loopback soak.</param>
/// <param name="SoakPayloadBytes">Per-packet payload body size for the loopback soak.</param>
/// <param name="StressIterations">Iteration count the M11 stress harness runs.</param>
/// <param name="StressReportDirectory">Optional explicit directory the stress runner
/// writes its paired report into. Null routes evidence to the default Opus stress
/// directory.</param>
/// <param name="KnownIssuesPath">Optional path to a JSON known-issue ledger the stress
/// harness loads and evaluates against blocker / must-fix thresholds.</param>
/// <param name="EnableStressNetworkInjection">True when the operator opted into the
/// M11.1 fault-injection network probe via any <c>--inject-*</c> option. False keeps
/// the stress runner on the legacy CPU/memory-only path.</param>
/// <param name="StressInjectionLossRate">Per-packet drop probability injected on every
/// client transport. Range <c>[0,1]</c>. Default <c>0.0</c>.</param>
/// <param name="StressInjectionLatencyMilliseconds">Added latency applied to outbound
/// packets before they reach the inner transport. Must be non-negative. Default
/// <c>0</c>.</param>
/// <param name="StressInjectionSeed">Deterministic RNG seed feeding the wrapping
/// transport's drop pattern.</param>
/// <param name="StressInjectionPeers">Per-iteration peer cohort the injection soak
/// drives.</param>
/// <param name="StressInjectionPacketsPerPeer">Per-iteration packets-per-peer count
/// the injection soak drives.</param>
/// <param name="StressInjectionPayloadBytes">Per-iteration payload body bytes per
/// packet.</param>
/// <param name="StressInjectionDropTolerance">Maximum outbound drop fraction the
/// stress harness tolerates before emitting <c>OPDX-STR-006</c>. Range <c>[0,1]</c>.</param>
/// <param name="StressInjectionInboundLossRate">Per-inbound-event drop probability
/// applied by the wrapping transport. Range <c>[0,1]</c>. Default <c>0.0</c>.</param>
/// <param name="StressInjectionInboundLatencyMilliseconds">Added latency applied to
/// inbound <c>Received</c> events. Default <c>0</c>.</param>
/// <param name="StressInjectionInboundSeed">Deterministic RNG seed feeding the
/// wrapping transport's inbound drop pattern.</param>
/// <param name="StressInjectionInboundDropTolerance">Maximum inbound drop fraction
/// the stress harness tolerates before emitting <c>OPDX-STR-006</c>. Range
/// <c>[0,1]</c>. Default <c>1.0</c> (effectively disabled — opt in via
/// <c>--inject-inbound-drop-tolerance</c>).</param>
/// <param name="KnownIssuesBasePath">Base ledger path consumed by
/// <c>known-issues-merge</c> (records are kept when the overlay does not override them).</param>
/// <param name="KnownIssuesOverlayPath">Overlay ledger path consumed by
/// <c>known-issues-merge</c> (records win on id collision).</param>
/// <param name="KnownIssuesLeftPath">Left ledger path consumed by
/// <c>known-issues-diff</c>.</param>
/// <param name="KnownIssuesRightPath">Right ledger path consumed by
/// <c>known-issues-diff</c>.</param>
/// <param name="KnownIssuesOutputPath">Output file path consumed by
/// <c>known-issues-merge</c> (mandatory) and <c>known-issues-diff</c> (optional —
/// when null the diff prints to stdout instead).</param>
/// <param name="KnownIssuesDiffFormat">Output shape for <c>known-issues-diff</c>.</param>
/// <param name="HelpReason">Free-form one-line explanation surfaced in help mode (e.g.
/// a parser error). Empty for normal usage banner.</param>
public sealed record OpusAlphaArgs(
    OpusAlphaMode Mode,
    AlphaSceneScale SceneScale,
    string? AssetPath,
    string? DiagnosticsDirectory,
    AlphaFaultKind InjectFailure,
    bool EnableFrameBudget,
    bool EnableAsyncLogging,
    DiagnosticOverlayLevel? OverlayLevel,
    string? SettingsPath,
    string? ConsumerAssemblyPath,
    int SmokeFrameCount,
    int? SmokeScreenshotFrame,
    string? SmokeReportPath,
    string? PackagePath,
    string? MachineReferencePath,
    string? MachineSavePath,
    int SoakPeers,
    int SoakPacketsPerPeer,
    int SoakPayloadBytes,
    int StressIterations,
    string? StressReportDirectory,
    string? KnownIssuesPath,
    bool EnableStressNetworkInjection,
    double StressInjectionLossRate,
    int StressInjectionLatencyMilliseconds,
    int StressInjectionSeed,
    int StressInjectionPeers,
    int StressInjectionPacketsPerPeer,
    int StressInjectionPayloadBytes,
    double StressInjectionDropTolerance,
    double StressInjectionInboundLossRate,
    int StressInjectionInboundLatencyMilliseconds,
    int StressInjectionInboundSeed,
    double StressInjectionInboundDropTolerance,
    string? KnownIssuesBasePath,
    string? KnownIssuesOverlayPath,
    string? KnownIssuesLeftPath,
    string? KnownIssuesRightPath,
    string? KnownIssuesOutputPath,
    KnownIssuesDiffFormat KnownIssuesDiffFormat,
    string HelpReason)
{
    /// <summary>Default smoke frame count (60) — one second at 60 Hz, matches
    /// <c>AlphaSmokeProfile.DefaultFrameTarget</c>.</summary>
    public const int DefaultSmokeFrameCount = 60;

    /// <summary>Default soak peer count (4) — matches <c>NetSoakProfile.Default.PeerCount</c>.</summary>
    public const int DefaultSoakPeers = 4;

    /// <summary>Default packets-per-peer for the soak (64).</summary>
    public const int DefaultSoakPacketsPerPeer = 64;

    /// <summary>Default soak payload (256 bytes).</summary>
    public const int DefaultSoakPayloadBytes = 256;

    /// <summary>Default stress iteration count (5) — matches
    /// <c>AlphaStressProfile.DefaultIterationCount</c>.</summary>
    public const int DefaultStressIterations = 5;

    /// <summary>Default deterministic RNG seed for the stress fault-injection layer.
    /// Pinned so unattended stress runs produce identical drop patterns across CI
    /// invocations.</summary>
    public const int DefaultStressInjectionSeed = 20260527;

    /// <summary>Default per-iteration peer cohort for the fault-injection soak.</summary>
    public const int DefaultStressInjectionPeers = 4;

    /// <summary>Default per-iteration packets-per-peer for the fault-injection soak.</summary>
    public const int DefaultStressInjectionPacketsPerPeer = 32;

    /// <summary>Default per-iteration payload-body bytes for the fault-injection soak.</summary>
    public const int DefaultStressInjectionPayloadBytes = 256;

    /// <summary>Default maximum drop fraction the stress harness tolerates before
    /// emitting <c>OPDX-STR-006</c>. Matches
    /// <c>AlphaStressFaultInjectionTolerance.Default.MaxDropRate</c>.</summary>
    public const double DefaultStressInjectionDropTolerance = 0.25;

    /// <summary>Default deterministic RNG seed for the inbound fault-injection layer.
    /// Independent from <see cref="DefaultStressInjectionSeed"/> so asymmetric drop
    /// patterns reproduce bit-for-bit.</summary>
    public const int DefaultStressInjectionInboundSeed = 20260528;

    /// <summary>Default maximum inbound drop fraction tolerated before
    /// <c>OPDX-STR-006</c>. <c>1.0</c> disables the inbound check until an operator
    /// passes <c>--inject-inbound-drop-tolerance</c> explicitly. Matches
    /// <c>AlphaStressFaultInjectionTolerance.Default.MaxInboundDropRate</c>.</summary>
    public const double DefaultStressInjectionInboundDropTolerance = 1.0;

    /// <summary>Returns a window-mode args record with defaults; tests use this to build
    /// up scenarios without spelling out every field.</summary>
    public static OpusAlphaArgs WindowDefaults() => new(
        Mode: OpusAlphaMode.Window,
        SceneScale: AlphaSceneScale.Small,
        AssetPath: null,
        DiagnosticsDirectory: null,
        InjectFailure: AlphaFaultKind.None,
        EnableFrameBudget: false,
        EnableAsyncLogging: false,
        OverlayLevel: null,
        SettingsPath: null,
        ConsumerAssemblyPath: null,
        SmokeFrameCount: DefaultSmokeFrameCount,
        SmokeScreenshotFrame: null,
        SmokeReportPath: null,
        PackagePath: null,
        MachineReferencePath: null,
        MachineSavePath: null,
        SoakPeers: DefaultSoakPeers,
        SoakPacketsPerPeer: DefaultSoakPacketsPerPeer,
        SoakPayloadBytes: DefaultSoakPayloadBytes,
        StressIterations: DefaultStressIterations,
        StressReportDirectory: null,
        KnownIssuesPath: null,
        EnableStressNetworkInjection: false,
        StressInjectionLossRate: 0.0,
        StressInjectionLatencyMilliseconds: 0,
        StressInjectionSeed: DefaultStressInjectionSeed,
        StressInjectionPeers: DefaultStressInjectionPeers,
        StressInjectionPacketsPerPeer: DefaultStressInjectionPacketsPerPeer,
        StressInjectionPayloadBytes: DefaultStressInjectionPayloadBytes,
        StressInjectionDropTolerance: DefaultStressInjectionDropTolerance,
        StressInjectionInboundLossRate: 0.0,
        StressInjectionInboundLatencyMilliseconds: 0,
        StressInjectionInboundSeed: DefaultStressInjectionInboundSeed,
        StressInjectionInboundDropTolerance: DefaultStressInjectionInboundDropTolerance,
        KnownIssuesBasePath: null,
        KnownIssuesOverlayPath: null,
        KnownIssuesLeftPath: null,
        KnownIssuesRightPath: null,
        KnownIssuesOutputPath: null,
        KnownIssuesDiffFormat: KnownIssuesDiffFormat.Text,
        HelpReason: string.Empty);

    /// <summary>Returns a help-mode args record with the supplied reason.</summary>
    public static OpusAlphaArgs Help(string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);
        return WindowDefaults() with
        {
            Mode = OpusAlphaMode.Help,
            HelpReason = reason,
        };
    }
}
