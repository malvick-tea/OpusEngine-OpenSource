using System.Linq;
using System.Numerics;
using FluentAssertions;
using Opus.Editor.Core;
using Opus.Foundation.Geometry;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class EditorFrameDrawerTests
{
    private static readonly Aabb UnitBox = new(new Vector3(-1f), new Vector3(1f));

    private static EditorFrameView ComposeWithSelection()
    {
        var document = new EditorDocument("Harbor");
        document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);
        return EditorFrameComposer.Compose(document, new OrbitCamera(), new FixedBounds(UnitBox), 1280, 720);
    }

    [Fact]
    public void Draw_clears_once_and_fills_every_chrome_panel()
    {
        var view = ComposeWithSelection();
        var surface = new RecordingDrawSurface();

        new EditorFrameDrawer().Draw(surface, view);

        surface.ClearCount.Should().Be(1);
        var panels = new[]
        {
            view.Chrome.Toolbar, view.Chrome.Viewport, view.Chrome.Outliner, view.Chrome.DslPanel, view.Chrome.StatusBar,
        };
        foreach (var panel in panels)
        {
            surface.Fills.Should().Contain(
                f => f.X == panel.X && f.Y == panel.Y && f.W == panel.Width && f.H == panel.Height);
        }
    }

    [Fact]
    public void Draw_renders_the_outliner_header_and_a_row_per_node()
    {
        var document = new EditorDocument("Harbor");
        document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);
        var view = EditorFrameComposer.Compose(document, new OrbitCamera(), new FixedBounds(UnitBox), 1280, 720);
        var surface = new RecordingDrawSurface();

        new EditorFrameDrawer().Draw(surface, view);

        surface.Texts.Select(t => t.Text).Should().Contain("scene tree");
        surface.Texts.Should().Contain(t => t.Text.Contains("alpha"));
    }

    [Fact]
    public void Draw_renders_the_stats_overlay_when_composed()
    {
        var document = new EditorDocument("Harbor");
        document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);
        var view = EditorFrameComposer.Compose(
            document, new OrbitCamera(), new FixedBounds(UnitBox), 1280, 720, statsVisible: true);
        var surface = new RecordingDrawSurface();

        new EditorFrameDrawer().Draw(surface, view);

        surface.Texts.Select(t => t.Text).Should().Contain("Stats (F3)");
        surface.Texts.Should().Contain(t => t.Text == "nodes");
        surface.Fills.Should().Contain(
            f => f.X == view.Stats!.Panel.X && f.Y == view.Stats.Panel.Y, "the stats panel fills its rect");
    }

    [Fact]
    public void Draw_emits_one_line_per_projected_viewport_line()
    {
        var view = ComposeWithSelection();
        var surface = new RecordingDrawSurface();

        new EditorFrameDrawer().Draw(surface, view);

        surface.Lines.Should().HaveCount(view.ViewportLines.Count);
        surface.Lines.Should().Contain(l => l.Thickness == 2, "the selection box is drawn thicker");
    }

    [Fact]
    public void Draw_renders_the_toolbar_status_and_pseudo_code_text()
    {
        var view = ComposeWithSelection();
        var surface = new RecordingDrawSurface();

        new EditorFrameDrawer().Draw(surface, view);

        surface.Texts.Select(t => t.Text).Should().Contain(view.ToolbarText);
        surface.Texts.Select(t => t.Text).Should().Contain(view.StatusText);
        surface.Texts.Should().Contain(t => t.Text.StartsWith("scene"));
    }

    [Fact]
    public void Draw_renders_the_localised_pseudo_code_header()
    {
        var view = EditorFrameComposer.Compose(
            new EditorDocument("Harbor"), new OrbitCamera(), new FixedBounds(UnitBox), 1280, 720,
            GizmoAxis.None, EditorChromeStrings.Russian);
        var surface = new RecordingDrawSurface();

        new EditorFrameDrawer().Draw(surface, view);

        surface.Texts.Select(t => t.Text).Should().Contain("псевдокод");
    }

    [Fact]
    public void The_help_overlay_draws_its_title_and_rows_when_visible()
    {
        var view = EditorFrameComposer.Compose(
            new EditorDocument("Harbor"), new OrbitCamera(), new NullBounds(), 1280, 720,
            GizmoAxis.None, helpVisible: true);
        var surface = new RecordingDrawSurface();

        new EditorFrameDrawer().Draw(surface, view);

        surface.Texts.Select(t => t.Text).Should().Contain("Shortcuts");
        surface.Texts.Select(t => t.Text).Should().Contain("Ctrl+R");
    }

    [Fact]
    public void The_pseudo_code_panel_clips_lines_that_overflow_its_height()
    {
        var document = new EditorDocument("Harbor");
        for (int i = 0; i < 200; i++)
        {
            document.PlaceNode($"n{i}", "models/tank.glb", EditorTransform.Identity);
        }

        var view = EditorFrameComposer.Compose(document, new OrbitCamera(), new FixedBounds(UnitBox), 1280, 720);
        var surface = new RecordingDrawSurface();

        new EditorFrameDrawer().Draw(surface, view);

        // Pseudo-code (and the panel header) are the only texts drawn inside the right-hand panel's column;
        // the toolbar and status text sit at the window's left edge.
        var panelTexts = surface.Texts.Where(t => t.X >= view.Chrome.DslPanel.X).ToList();
        panelTexts.Should().OnlyContain(t => t.Y < view.Chrome.DslPanel.Bottom);
        panelTexts.Count.Should().BeLessThan(view.PseudoCodeLines.Count, "the long mirror is clipped to the panel");
    }
}
