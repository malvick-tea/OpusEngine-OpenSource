using System;
using FluentAssertions;
using Opus.Engine.Diagnostics.Overlay;
using Xunit;

namespace Opus.Engine.Diagnostics.Tests.Overlay;

public sealed class DiagnosticPanelTests
{
    [Fact]
    public void Panel_create_rejects_null_row_entry()
    {
        var rows = new[]
        {
            DiagnosticRow.Create("a", "1", DiagnosticRowKind.Text),
            null!,
        };

        var act = () => DiagnosticPanel.Create(DiagnosticPanelKind.Runtime, "runtime", rows);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Panel_create_rejects_empty_title()
    {
        var act = () => DiagnosticPanel.Create(
            DiagnosticPanelKind.Runtime,
            "  ",
            new[] { DiagnosticRow.Create("a", "1", DiagnosticRowKind.Text) });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Snapshot_create_rejects_null_panel_entry()
    {
        var panel = DiagnosticPanel.Create(
            DiagnosticPanelKind.Runtime,
            "runtime",
            new[] { DiagnosticRow.Create("a", "1", DiagnosticRowKind.Text) });
        var panels = new[] { panel, null! };

        var act = () => DiagnosticOverlaySnapshot.Create(panels);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Snapshot_create_uses_shared_empty_for_zero_panels()
    {
        var snapshot = DiagnosticOverlaySnapshot.Create(Array.Empty<DiagnosticPanel>());

        snapshot.Should().BeSameAs(DiagnosticOverlaySnapshot.Empty);
    }
}
