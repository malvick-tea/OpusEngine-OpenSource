using System;
using System.Collections.Generic;
using FluentAssertions;
using Opus.Engine.Diagnostics.Overlay;
using Opus.Foundation;
using Xunit;

namespace Opus.Engine.Diagnostics.Tests.Overlay;

public sealed class DiagnosticOverlayInputsTests
{
    [Fact]
    public void Create_defaults_consumer_panels_to_empty_not_null()
    {
        var inputs = NewInputs(consumerPanels: null);

        inputs.ConsumerPanels.Should().NotBeNull();
        inputs.ConsumerPanels.Should().BeEmpty();
    }

    [Fact]
    public void Create_filters_null_consumer_panel_entries()
    {
        var inputs = NewInputs(consumerPanels: new[] { ConsumerPanel("kept"), null! });

        inputs.ConsumerPanels.Should().ContainSingle(panel => panel.Title == "kept");
    }

    [Fact]
    public void Create_copies_consumer_panels_defensively()
    {
        var source = new List<DiagnosticPanel> { ConsumerPanel("first") };

        var inputs = NewInputs(consumerPanels: source);
        source.Add(ConsumerPanel("added-after-create"));

        inputs.ConsumerPanels.Should().ContainSingle(panel => panel.Title == "first");
    }

    private static DiagnosticOverlayInputs NewInputs(IReadOnlyList<DiagnosticPanel>? consumerPanels) =>
        DiagnosticOverlayInputs.Create(
            BuildInfo.Current,
            new DiagnosticFrameMetrics(
                SampleCount: 1,
                TotalFramesObserved: 1,
                Mean: TimeSpan.FromMilliseconds(10),
                Min: TimeSpan.FromMilliseconds(10),
                Max: TimeSpan.FromMilliseconds(10),
                P95: TimeSpan.FromMilliseconds(10)),
            DiagnosticAdapterSnapshot.Create("Test Adapter", 1280, 720, 1024, 640),
            DiagnosticContentSnapshot.Create(1, 1, @"C:\tmp\alpha.glb", usesProceduralFallback: false),
            DiagnosticNetworkSnapshot.Create(DiagnosticNetworkState.Unavailable, "test hook"),
            lastScreenshotPath: null,
            new DateTimeOffset(2026, 5, 29, 0, 0, 0, TimeSpan.Zero),
            consumerPanels);

    private static DiagnosticPanel ConsumerPanel(string title) => DiagnosticPanel.Create(
        DiagnosticPanelKind.Content,
        title,
        new[] { DiagnosticRow.Create("key", "value", DiagnosticRowKind.Text) });
}
