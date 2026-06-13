using System;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class EditorSceneBrowserTests
{
    private static readonly EditorPanelRect Viewport = new(0, 32, 960, 640);

    private static readonly string[] ThreeFiles =
    {
        @"C:\work\alpha.scene.json",
        @"C:\work\bravo.scene.json",
        @"C:\work\charlie.scene.json",
    };

    [Fact]
    public void Build_lists_one_row_per_file_with_file_names_and_the_highlight()
    {
        var view = EditorSceneBrowser.Build(
            Viewport, new SceneBrowserState(ThreeFiles, 1), EditorChromeStrings.English);

        view.Rows.Should().HaveCount(3);
        view.Rows.Select(r => r.Label).Should().Equal("alpha.scene.json", "bravo.scene.json", "charlie.scene.json");
        view.Rows.Select(r => r.Path).Should().Equal(ThreeFiles);
        view.Rows.Count(r => r.Highlighted).Should().Be(1);
        view.Rows[1].Highlighted.Should().BeTrue();
    }

    [Fact]
    public void A_model_browser_shows_whole_refs_under_its_own_title()
    {
        var refs = new[] { "models/tank.glb", "props/crate.glb" };

        var view = EditorSceneBrowser.Build(
            Viewport, new SceneBrowserState(refs, 0, BrowserPurpose.PlaceModel), EditorChromeStrings.English);

        view.Title.Should().Be(EditorChromeStrings.English.ModelBrowserTitle);
        view.Rows.Select(r => r.Label).Should().Equal("models/tank.glb", "props/crate.glb");
    }

    [Fact]
    public void An_empty_model_browser_carries_the_model_hint()
    {
        var view = EditorSceneBrowser.Build(
            Viewport,
            new SceneBrowserState(Array.Empty<string>(), 0, BrowserPurpose.PlaceModel),
            EditorChromeStrings.English);

        view.Rows.Should().BeEmpty();
        view.EmptyHint.Should().Be(EditorChromeStrings.English.ModelBrowserEmpty);
    }

    [Fact]
    public void A_long_listing_scrolls_to_keep_the_highlight_visible()
    {
        var many = Enumerable.Range(0, 80).Select(i => $@"C:\w\scene-{i:D2}.scene.json").ToArray();

        var view = EditorSceneBrowser.Build(
            Viewport, new SceneBrowserState(many, 50), EditorChromeStrings.English);

        view.Rows.Should().Contain(r => r.Highlighted, "the highlighted entry is always on screen");
        view.Rows[^1].Path.Should().Be(many[50], "the window scrolls just far enough to reach the highlight");
    }

    [Fact]
    public void The_scroll_window_never_passes_the_last_full_page()
    {
        var many = Enumerable.Range(0, 40).Select(i => $@"C:\w\scene-{i:D2}.scene.json").ToArray();

        var view = EditorSceneBrowser.Build(
            Viewport, new SceneBrowserState(many, 39), EditorChromeStrings.English);

        view.Rows[^1].Path.Should().Be(many[39], "the last entry is reachable");
        int capacity = EditorSceneBrowser.VisibleRowCapacity(view.Panel);
        view.Rows.Should().HaveCount(capacity, "the last page is full, not a lonely tail");
    }

    [Fact]
    public void A_short_listing_starts_at_the_top_regardless_of_highlight()
    {
        EditorSceneBrowser.FirstVisibleIndex(count: 3, highlight: 2, capacity: 10).Should().Be(0);
        EditorSceneBrowser.FirstVisibleIndex(count: 30, highlight: 0, capacity: 10).Should().Be(0);
        EditorSceneBrowser.FirstVisibleIndex(count: 30, highlight: 14, capacity: 10).Should().Be(5);
        EditorSceneBrowser.FirstVisibleIndex(count: 30, highlight: 29, capacity: 10).Should().Be(20);
        EditorSceneBrowser.FirstVisibleIndex(count: 30, highlight: 5, capacity: 0).Should().Be(0);
    }

    [Fact]
    public void The_panel_centres_inside_the_viewport()
    {
        var view = EditorSceneBrowser.Build(
            Viewport, new SceneBrowserState(ThreeFiles, 0), EditorChromeStrings.English);

        var panel = view.Panel;
        panel.X.Should().BeGreaterThan(Viewport.X);
        panel.Right.Should().BeLessThan(Viewport.Right);
        panel.Y.Should().BeGreaterThan(Viewport.Y);
        panel.Bottom.Should().BeLessThan(Viewport.Bottom);
    }

    [Fact]
    public void An_empty_listing_builds_no_rows_but_keeps_the_hint()
    {
        var view = EditorSceneBrowser.Build(
            Viewport, new SceneBrowserState(Array.Empty<string>(), 0), EditorChromeStrings.English);

        view.Rows.Should().BeEmpty();
        view.EmptyHint.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void An_out_of_range_highlight_clamps_onto_a_valid_row()
    {
        var view = EditorSceneBrowser.Build(
            Viewport, new SceneBrowserState(ThreeFiles, 99), EditorChromeStrings.English);

        view.Rows[2].Highlighted.Should().BeTrue();
    }

    [Fact]
    public void Hit_test_returns_the_row_index_under_the_pixel()
    {
        var view = EditorSceneBrowser.Build(
            Viewport, new SceneBrowserState(ThreeFiles, 0), EditorChromeStrings.English);
        var second = view.Rows[1].Rect;

        EditorSceneBrowser.HitTest(view.Rows, second.X + 5, second.Y + 5).Should().Be(1);
        EditorSceneBrowser.HitTest(view.Rows, Viewport.X + 1, Viewport.Y + 1).Should().Be(-1);
    }

    [Fact]
    public void Rows_clip_when_the_viewport_is_short()
    {
        var shortViewport = new EditorPanelRect(
            0, 0, 960, EditorSceneBrowser.HeaderHeight + (2 * EditorSceneBrowser.RowHeight) + (2 * EditorSceneBrowser.ViewportMargin));

        var view = EditorSceneBrowser.Build(
            shortViewport, new SceneBrowserState(ThreeFiles, 0), EditorChromeStrings.English);

        view.Rows.Count.Should().BeLessThan(3);
    }
}
