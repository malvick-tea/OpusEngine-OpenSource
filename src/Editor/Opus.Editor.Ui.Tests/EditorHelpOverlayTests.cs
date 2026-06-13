using System.Linq;
using FluentAssertions;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class EditorHelpOverlayTests
{
    private static readonly EditorPanelRect Viewport = new(0, 32, 960, 664);

    [Fact]
    public void The_table_is_localised_per_language()
    {
        var english = EditorHelpOverlay.Entries(EditorLanguage.English);
        var russian = EditorHelpOverlay.Entries(EditorLanguage.Russian);

        english.Should().NotBeEmpty();
        russian.Should().HaveCount(english.Count, "both languages document the same shortcuts");
        EditorHelpOverlay.Title(EditorLanguage.Russian).Should().NotBe(EditorHelpOverlay.Title(EditorLanguage.English));
        russian.Select(e => e.Description).Should().NotIntersectWith(english.Select(e => e.Description));
    }

    [Fact]
    public void Every_window_shortcut_appears_in_the_table()
    {
        var keys = EditorHelpOverlay.Entries(EditorLanguage.English).Select(e => e.Keys).ToList();

        foreach (var expected in new[]
        {
            "A", "L", "W / E / R", "Arrows", "F", "Del", "P / Shift+P", "Ctrl+Click", "Shift+Drag", "Ctrl+A",
            "Ctrl+R", "Ctrl+D", "Ctrl+G", "Ctrl+C / Ctrl+V", "Ctrl+S", "Ctrl+Shift+S", "F1", "F2", "F3", "Esc",
        })
        {
            keys.Should().Contain(expected);
        }
    }

    [Fact]
    public void The_panel_is_centred_inside_the_viewport()
    {
        var help = EditorHelpOverlay.Build(Viewport, EditorLanguage.English);

        help.Panel.X.Should().BeGreaterThanOrEqualTo(Viewport.X);
        help.Panel.Right.Should().BeLessThanOrEqualTo(Viewport.Right);
        help.Panel.Y.Should().BeGreaterThanOrEqualTo(Viewport.Y);
        help.Panel.Bottom.Should().BeLessThanOrEqualTo(Viewport.Bottom);
        (help.Panel.X - Viewport.X).Should().Be(Viewport.Right - help.Panel.Right, "horizontally centred");
    }

    [Fact]
    public void A_tiny_viewport_clamps_the_panel_instead_of_overflowing()
    {
        var tiny = new EditorPanelRect(0, 0, 200, 120);

        var help = EditorHelpOverlay.Build(tiny, EditorLanguage.English);

        help.Panel.Right.Should().BeLessThanOrEqualTo(tiny.Right);
        help.Panel.Bottom.Should().BeLessThanOrEqualTo(tiny.Bottom);
    }
}
