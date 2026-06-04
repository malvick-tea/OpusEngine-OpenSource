using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Opus.Engine.Diagnostics.Overlay;
using Opus.Foundation;
using Xunit;

namespace Opus.Engine.Diagnostics.Tests.Overlay;

public sealed class DiagnosticOverlayComposerTests
{
    [Fact]
    public void Compose_returns_empty_snapshot_when_overlay_is_off()
    {
        var composer = new DiagnosticOverlayComposer();
        var options = DiagnosticOverlayOptions.Default with { Level = DiagnosticOverlayLevel.Off };

        var snapshot = composer.Compose(NewInputs(), options);

        snapshot.Should().BeSameAs(DiagnosticOverlaySnapshot.Empty);
    }

    [Fact]
    public void Compose_returns_empty_snapshot_when_overlay_is_disabled()
    {
        var composer = new DiagnosticOverlayComposer();
        var options = DiagnosticOverlayOptions.Default with { Enabled = false };

        var snapshot = composer.Compose(NewInputs(), options);

        snapshot.Should().BeSameAs(DiagnosticOverlaySnapshot.Empty);
    }

    [Fact]
    public void Compose_minimal_snapshot_contains_runtime_and_frame_panels()
    {
        var composer = new DiagnosticOverlayComposer();
        var options = DiagnosticOverlayOptions.Default with { Level = DiagnosticOverlayLevel.Minimal };

        var snapshot = composer.Compose(NewInputs(), options);

        snapshot.Panels.Select(panel => panel.Kind).Should().Equal(
            DiagnosticPanelKind.Runtime,
            DiagnosticPanelKind.Frame);
    }

    [Fact]
    public void Compose_full_snapshot_contains_all_four_panel_kinds_in_order()
    {
        var composer = new DiagnosticOverlayComposer();

        var snapshot = composer.Compose(NewInputs(), DiagnosticOverlayOptions.Default);

        snapshot.Panels.Select(panel => panel.Kind).Should().Equal(
            DiagnosticPanelKind.Runtime,
            DiagnosticPanelKind.Frame,
            DiagnosticPanelKind.Content,
            DiagnosticPanelKind.Network);
    }

    [Fact]
    public void Compose_full_snapshot_includes_screenshot_row_with_file_name()
    {
        var composer = new DiagnosticOverlayComposer();

        var snapshot = composer.Compose(NewInputs(), DiagnosticOverlayOptions.Default);

        snapshot.Panels.SelectMany(panel => panel.Rows).Should().Contain(row =>
            row.Kind == DiagnosticRowKind.Path && row.Value == "capture.png");
    }

    [Fact]
    public void Compose_full_snapshot_includes_adapter_vram_and_vendor_rows()
    {
        var composer = new DiagnosticOverlayComposer();

        var snapshot = composer.Compose(NewInputs(), DiagnosticOverlayOptions.Default);

        var rows = snapshot.Panels.SelectMany(panel => panel.Rows).ToList();
        rows.Should().Contain(row => row.Label == "vram" && row.Value == "8192 MB");
        rows.Should().Contain(row => row.Label == "vendor" && row.Value == "NVIDIA (discrete)");
    }

    [Fact]
    public void Compose_uses_procedural_label_when_fallback_asset_is_active()
    {
        var composer = new DiagnosticOverlayComposer();
        var inputs = NewInputs(usesProceduralFallback: true);

        var snapshot = composer.Compose(inputs, DiagnosticOverlayOptions.Default);

        snapshot.Panels.SelectMany(panel => panel.Rows).Should().Contain(row =>
            row.Kind == DiagnosticRowKind.State
            && row.Value == DiagnosticOverlayComposer.ProceduralAssetLabel);
    }

    [Fact]
    public void Compose_respects_max_row_cap()
    {
        var composer = new DiagnosticOverlayComposer();
        var options = DiagnosticOverlayOptions.Default with { MaxRows = 3 };

        var snapshot = composer.Compose(NewInputs(), options);

        snapshot.Panels.Sum(panel => panel.Rows.Count).Should().Be(3);
    }

    [Fact]
    public void Compose_full_appends_consumer_panels_after_engine_panels()
    {
        var composer = new DiagnosticOverlayComposer();
        var inputs = NewInputs(consumerPanels: new[]
        {
            ConsumerPanel("consumer-vehicles"),
            ConsumerPanel("consumer-objectives"),
        });

        var snapshot = composer.Compose(inputs, DiagnosticOverlayOptions.Default with { MaxRows = 64 });

        snapshot.Panels.Select(panel => panel.Title).Should().Equal(
            DiagnosticOverlayComposer.RuntimePanelTitle,
            DiagnosticOverlayComposer.FramePanelTitle,
            DiagnosticOverlayComposer.ContentPanelTitle,
            DiagnosticOverlayComposer.NetworkPanelTitle,
            "consumer-vehicles",
            "consumer-objectives");
    }

    [Fact]
    public void Compose_minimal_omits_consumer_panels()
    {
        var composer = new DiagnosticOverlayComposer();
        var inputs = NewInputs(consumerPanels: new[] { ConsumerPanel("consumer-vehicles") });
        var options = DiagnosticOverlayOptions.Default with { Level = DiagnosticOverlayLevel.Minimal };

        var snapshot = composer.Compose(inputs, options);

        snapshot.Panels.Select(panel => panel.Title)
            .Should().NotContain("consumer-vehicles");
    }

    [Fact]
    public void Compose_consumer_panels_are_dropped_first_under_a_tight_row_cap()
    {
        var composer = new DiagnosticOverlayComposer();
        var inputs = NewInputs(consumerPanels: new[] { ConsumerPanel("consumer-vehicles") });
        var options = DiagnosticOverlayOptions.Default with { MaxRows = 3 };

        var snapshot = composer.Compose(inputs, options);

        snapshot.Panels.Sum(panel => panel.Rows.Count).Should().Be(3);
        snapshot.Panels.Select(panel => panel.Title).Should().NotContain("consumer-vehicles");
    }

    [Fact]
    public void Compose_rejects_null_inputs()
    {
        var composer = new DiagnosticOverlayComposer();

        var act = () => composer.Compose(inputs: null!, DiagnosticOverlayOptions.Default);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Compose_rejects_null_options()
    {
        var composer = new DiagnosticOverlayComposer();

        var act = () => composer.Compose(NewInputs(), options: null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static DiagnosticOverlayInputs NewInputs(
        bool usesProceduralFallback = false,
        IReadOnlyList<DiagnosticPanel>? consumerPanels = null) =>
        DiagnosticOverlayInputs.Create(
            BuildInfo.Current,
            new DiagnosticFrameMetrics(
                SampleCount: 4,
                TotalFramesObserved: 12,
                Mean: TimeSpan.FromMilliseconds(10),
                Min: TimeSpan.FromMilliseconds(7),
                Max: TimeSpan.FromMilliseconds(18),
                P95: TimeSpan.FromMilliseconds(17)),
            DiagnosticAdapterSnapshot.Create(
                "Test Adapter",
                1280,
                720,
                1024,
                640,
                DiagnosticAdapterHardware.Create(
                    "NVIDIA",
                    vendorId: 0x10DE,
                    deviceId: 0x2684,
                    dedicatedVideoMemoryBytes: 8L * 1024 * 1024 * 1024,
                    adapterClass: DiagnosticAdapterClass.Discrete)),
            DiagnosticContentSnapshot.Create(8, 92, @"C:\tmp\alpha.glb", usesProceduralFallback),
            DiagnosticNetworkSnapshot.Create(DiagnosticNetworkState.Degraded, "test hook"),
            "capture.png",
            new DateTimeOffset(2026, 5, 26, 12, 0, 0, TimeSpan.Zero),
            consumerPanels);

    private static DiagnosticPanel ConsumerPanel(string title) => DiagnosticPanel.Create(
        DiagnosticPanelKind.Content,
        title,
        new[] { DiagnosticRow.Create("key", "value", DiagnosticRowKind.Text) });
}
