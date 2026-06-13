using FluentAssertions;
using Opus.Editor.Ui;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class EditorChromeStringsTests
{
    [Fact]
    public void English_is_the_default_chrome()
    {
        EditorChromeStrings.For(EditorLanguage.English).Should().BeSameAs(EditorChromeStrings.English);
    }

    [Fact]
    public void Russian_chrome_uses_cyrillic_labels()
    {
        var russian = EditorChromeStrings.For(EditorLanguage.Russian);

        russian.Should().BeSameAs(EditorChromeStrings.Russian);
        russian.PseudoCodeHeader.Should().Be("псевдокод");
        russian.NoSelection.Should().Be("нет выделения");
    }

    [Fact]
    public void Gizmo_mode_names_are_localised()
    {
        EditorChromeStrings.English.GizmoModeName(GizmoMode.Translate).Should().Be("move");
        EditorChromeStrings.English.GizmoModeName(GizmoMode.Rotate).Should().Be("rotate");
        EditorChromeStrings.Russian.GizmoModeName(GizmoMode.Scale).Should().Be("масштаб");
    }
}
