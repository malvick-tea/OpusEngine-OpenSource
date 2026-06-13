using FluentAssertions;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class EditorChromeLayoutTests
{
    [Fact]
    public void Toolbar_and_status_bar_span_the_full_width_at_top_and_bottom()
    {
        var chrome = EditorChromeLayout.Build(1280, 720);

        chrome.Toolbar.X.Should().Be(0);
        chrome.Toolbar.Y.Should().Be(0);
        chrome.Toolbar.Width.Should().Be(1280);
        chrome.Toolbar.Height.Should().Be(EditorChromeLayout.ToolbarHeight);

        chrome.StatusBar.X.Should().Be(0);
        chrome.StatusBar.Width.Should().Be(1280);
        chrome.StatusBar.Bottom.Should().Be(720);
        chrome.StatusBar.Height.Should().Be(EditorChromeLayout.StatusBarHeight);
    }

    [Fact]
    public void Viewport_and_dsl_panel_partition_the_body_without_overlap()
    {
        var chrome = EditorChromeLayout.Build(1280, 720);

        chrome.Viewport.Y.Should().Be(EditorChromeLayout.ToolbarHeight);
        chrome.Viewport.Bottom.Should().Be(chrome.StatusBar.Y);
        chrome.DslPanel.Width.Should().Be(EditorChromeLayout.DslPanelWidth);
        chrome.Viewport.Right.Should().Be(chrome.DslPanel.X);
        chrome.DslPanel.Right.Should().Be(1280);
        chrome.Viewport.Width.Should().Be(1280 - EditorChromeLayout.DslPanelWidth);
    }

    [Fact]
    public void The_right_column_splits_into_outliner_inspector_then_pseudo_code()
    {
        var chrome = EditorChromeLayout.Build(1280, 720);

        chrome.Outliner.X.Should().Be(chrome.DslPanel.X);
        chrome.Outliner.Width.Should().Be(chrome.DslPanel.Width);
        chrome.Inspector.X.Should().Be(chrome.DslPanel.X);
        chrome.Inspector.Width.Should().Be(chrome.DslPanel.Width);
        chrome.Outliner.Y.Should().Be(EditorChromeLayout.ToolbarHeight);
        chrome.Outliner.Bottom.Should().Be(chrome.Inspector.Y, "the outliner sits directly above the inspector");
        chrome.Inspector.Bottom.Should().Be(chrome.DslPanel.Y, "the inspector sits directly above the mirror");
        chrome.DslPanel.Bottom.Should().Be(chrome.StatusBar.Y);
        chrome.Outliner.Height.Should().BeGreaterThanOrEqualTo(EditorChromeLayout.MinRightColumnPanelHeight);
        chrome.DslPanel.Height.Should().BeGreaterThanOrEqualTo(EditorChromeLayout.MinRightColumnPanelHeight);
    }

    [Fact]
    public void The_inspector_gets_its_preferred_height_on_a_normal_window()
    {
        var chrome = EditorChromeLayout.Build(1280, 720);

        chrome.Inspector.Height.Should().Be(EditorChromeLayout.PreferredInspectorHeight);
    }

    [Fact]
    public void A_below_minimum_size_is_clamped_to_a_usable_layout()
    {
        var chrome = EditorChromeLayout.Build(10, 10);

        chrome.Toolbar.Width.Should().Be(EditorChromeLayout.MinWindowWidth);
        chrome.Viewport.Width.Should().BeGreaterThanOrEqualTo(EditorChromeLayout.MinViewportWidth);
        chrome.Viewport.Height.Should().BeGreaterThanOrEqualTo(EditorChromeLayout.MinViewportHeight);
    }

    [Fact]
    public void A_narrow_window_never_starves_the_viewport_below_its_minimum()
    {
        var chrome = EditorChromeLayout.Build(EditorChromeLayout.MinWindowWidth, 720);

        chrome.Viewport.Width.Should().Be(EditorChromeLayout.MinViewportWidth);
        chrome.DslPanel.Width.Should().Be(EditorChromeLayout.DslPanelWidth);
    }
}
