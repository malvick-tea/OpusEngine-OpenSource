using System;
using Opus.Engine.AlphaHarness.Scenes;
using Opus.Engine.Consumer.Integration;
using Opus.Engine.Diagnostics.Overlay;
using Opus.Engine.Diagnostics.Reports;
using Opus.Engine.Host.Windows.Direct3D12.Frame;
using Opus.Engine.Net.Telemetry;
using Opus.Foundation;

namespace Opus.Engine.Host.Windows.Direct3D12;

/// <summary>
/// Construction-time configuration for the runtime D3D12 host application.
/// </summary>
/// <param name="WindowTitle">
/// Title bar text for the live D3D12 window. Defaults to the Opus identity line so a
/// running sample announces itself.
/// </param>
/// <param name="WindowWidth">Initial window width in pixels.</param>
/// <param name="WindowHeight">Initial window height in pixels.</param>
/// <param name="EnableDebugLayer">
/// Enable the D3D12 debug layer at device creation. Off by default — the layer adds
/// significant CPU overhead and is enabled for diagnostics passes, not for sample runs.
/// </param>
/// <param name="AssetPath">
/// Optional path to a glTF/GLB asset the alpha frame loads as the tank marker.
/// When null the host generates a procedural sample via
/// <see cref="Content.Sample.SampleAlphaTankGltfWriter"/> and renders that instead.
/// The engine never bundles game assets; consumer projects supply their own.
/// </param>
/// <param name="MetricsWindow">
/// Maximum number of recent <c>CpuFrameTime</c> samples retained by the rolling metrics
/// aggregator. Larger values smooth out noise but cost memory at sample-time; 240 ≈ 4 s
/// at 60 Hz is the default and matches the Opus 0.1 alpha-frame budget envelope.
/// </param>
/// <param name="DiagnosticOverlay">Optional diagnostic overlay configuration.</param>
/// <param name="DiagnosticsDirectory">Optional root directory for tester evidence.</param>
/// <param name="NetTelemetryProvider">
/// Optional callback returning a fresh <see cref="NetSessionTelemetry"/> snapshot every
/// time the overlay refreshes. The engine itself owns no network session — Opus is
/// genre-neutral — but the host plumbing accepts a consumer-supplied provider so a
/// game-side session can surface state, peer counts, sent/received counters, and
/// reconnect evidence in the tester overlay. <c>null</c> means "no telemetry";
/// the overlay falls back to <see cref="DiagnosticNetworkSnapshot.NotConfigured"/>.
/// </param>
/// <param name="SceneScale">
/// Named scene-density preset routed into <c>D3D12AlphaFramePlan</c>. <c>Small</c> matches
/// the M5/M5.1 baseline (~100 repeated actors); <c>Large</c> opens the M9 large-map smoke
/// shape (~400 repeated actors) for camera, batching, memory, and frame-pacing pressure.
/// </param>
/// <param name="ConsumerIntegration">
/// Optional engine-neutral consumer registration. When supplied, the D3D12 host routes
/// lifecycle hooks, scene-source capture, asset-catalog resolution, and telemetry through
/// this facade. Null preserves the M9 alpha-host behaviour.
/// </param>
/// <param name="FrameBudget">
/// Alpha-frame budget watchdog policy. <see cref="D3D12AlphaFrameBudgetPolicy.Disabled"/>
/// preserves the pre-M12 host behaviour (metrics observed, no enforcement). When enabled
/// the host folds per-frame CPU times into a rolling window and emits a structured warning
/// each time the window breaches the configured <c>FramePacingThresholds</c>. Closes the
/// M5.1 lead follow-up.
/// </param>
/// <param name="Resizable">
/// Whether the host opens a resizable window and tracks the swap chain + scene viewport to
/// the live client size. <c>true</c> by default so the alpha host honours the Opus 0.1
/// "resize behavior is dependable" requirement; set <c>false</c> for a fixed-size run (e.g.
/// deterministic screenshot capture). When false the window cannot be drag-resized, so the
/// resize path never fires.
/// </param>
public sealed record D3D12OpusApplicationOptions(
    string WindowTitle,
    int WindowWidth,
    int WindowHeight,
    bool EnableDebugLayer = false,
    string? AssetPath = null,
    int MetricsWindow = 240,
    DiagnosticOverlayOptions? DiagnosticOverlay = null,
    string? DiagnosticsDirectory = null,
    Func<NetSessionTelemetry?>? NetTelemetryProvider = null,
    AlphaSceneScale SceneScale = AlphaSceneScale.Small,
    ConsumerIntegration? ConsumerIntegration = null,
    D3D12AlphaFrameBudgetPolicy? FrameBudget = null,
    bool Resizable = true)
{
    public const int DefaultWindowWidth = 1280;
    public const int DefaultWindowHeight = 720;

    /// <summary>Default <see cref="MetricsWindow"/> length: 240 samples ≈ 4 s at 60 Hz,
    /// matching the Opus 0.1 alpha-frame budget envelope. Mirrored as the literal default
    /// on the primary constructor since C# does not let a primary-ctor parameter default
    /// reference a const declared in the same record body.</summary>
    public const int DefaultMetricsWindow = 240;

    /// <summary>Default options sized for the Opus 0.1 sample host: 1280×720 windowed,
    /// product-banner title, no debug layer, procedural sample asset, 240-frame metrics
    /// window.</summary>
    public static D3D12OpusApplicationOptions Default { get; } = new(
        WindowTitle: EngineIdentity.Current.DisplayName + " — Alpha Host",
        WindowWidth: DefaultWindowWidth,
        WindowHeight: DefaultWindowHeight);

    public DiagnosticOverlayOptions EffectiveDiagnosticOverlayOptions =>
        DiagnosticOverlay ?? DiagnosticOverlayOptions.Default;

    public string EffectiveDiagnosticsDirectory =>
        DiagnosticsDirectory ?? OpusDiagnosticsPaths.DefaultRootDirectory();

    /// <summary>Resolves the nullable <see cref="FrameBudget"/> field to a concrete
    /// policy. Returns <see cref="D3D12AlphaFrameBudgetPolicy.Disabled"/> when the option
    /// is unset so callers can read the policy without null checks.</summary>
    public D3D12AlphaFrameBudgetPolicy EffectiveFrameBudget =>
        FrameBudget ?? D3D12AlphaFrameBudgetPolicy.Disabled;

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(WindowTitle))
        {
            throw new ArgumentException("WindowTitle must not be empty.", nameof(WindowTitle));
        }

        if (WindowWidth < 160 || WindowHeight < 120)
        {
            throw new ArgumentOutOfRangeException(
                nameof(WindowWidth),
                "D3D12 alpha host needs at least a 160x120 window.");
        }

        if (MetricsWindow < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MetricsWindow),
                "MetricsWindow must be at least 1.");
        }

        if (DiagnosticsDirectory is not null && string.IsNullOrWhiteSpace(DiagnosticsDirectory))
        {
            throw new ArgumentException(
                "DiagnosticsDirectory must not be empty when supplied.",
                nameof(DiagnosticsDirectory));
        }

        EffectiveDiagnosticOverlayOptions.Validate();
        EffectiveFrameBudget.Validate();
    }
}
