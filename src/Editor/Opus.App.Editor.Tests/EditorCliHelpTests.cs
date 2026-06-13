using FluentAssertions;
using Opus.App.Editor.Cli;
using Xunit;

namespace Opus.App.Editor.Tests;

public sealed class EditorCliHelpTests
{
    [Fact]
    public void Help_lists_the_commands()
    {
        var help = EditorCliHelp.Render(string.Empty);

        help.Should().Contain("new");
        help.Should().Contain("show");
        help.Should().Contain("dsl");
        help.Should().Contain("materials");
        help.Should().Contain("anim-state");
        help.Should().Contain("project-add");
        help.Should().Contain("scene-move");
        help.Should().Contain("scene-parent");
        help.Should().Contain("scene-unparent");
        help.Should().Contain("light-add");
        help.Should().Contain("light-remove");
        help.Should().Contain("light-edit");
        help.Should().Contain("--project");
    }

    [Fact]
    public void Help_prefixes_the_error_reason()
    {
        EditorCliHelp.Render("boom").Should().StartWith("error: boom");
    }
}
