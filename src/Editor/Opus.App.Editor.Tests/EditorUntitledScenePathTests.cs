using System.IO;
using FluentAssertions;
using Opus.App.Editor.Run;
using Xunit;

namespace Opus.App.Editor.Tests;

public sealed class EditorUntitledScenePathTests
{
    [Fact]
    public void The_first_untitled_save_uses_the_plain_name()
    {
        EditorUntitledScenePath.Next("work", _ => false)
            .Should().Be(Path.Combine("work", "untitled.scene.json"));
    }

    [Fact]
    public void Taken_names_are_skipped_with_a_numeric_suffix()
    {
        var taken = new[]
        {
            Path.Combine("work", "untitled.scene.json"),
            Path.Combine("work", "untitled-2.scene.json"),
        };

        EditorUntitledScenePath.Next("work", path => System.Array.IndexOf(taken, path) >= 0)
            .Should().Be(Path.Combine("work", "untitled-3.scene.json"));
    }
}
