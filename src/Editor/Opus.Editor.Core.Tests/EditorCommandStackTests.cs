using FluentAssertions;
using Xunit;

namespace Opus.Editor.Core.Tests;

public sealed class EditorCommandStackTests
{
    private static PlaceNodeCommand Place(EditorScene scene, string name) =>
        new(new SceneNode(scene.AllocateId(), name, null, EditorTransform.Identity));

    [Fact]
    public void Execute_then_undo_then_redo_round_trips()
    {
        var scene = new EditorScene();
        var stack = new EditorCommandStack(scene);

        stack.Execute(Place(scene, "a"));
        scene.Count.Should().Be(1);
        stack.CanUndo.Should().BeTrue();

        stack.Undo().Should().BeTrue();
        scene.Count.Should().Be(0);
        stack.CanRedo.Should().BeTrue();

        stack.Redo().Should().BeTrue();
        scene.Count.Should().Be(1);
    }

    [Fact]
    public void Executing_a_new_command_clears_the_redo_branch()
    {
        var scene = new EditorScene();
        var stack = new EditorCommandStack(scene);

        stack.Execute(Place(scene, "a"));
        stack.Undo();
        stack.CanRedo.Should().BeTrue();

        stack.Execute(Place(scene, "b"));

        stack.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void History_lists_applied_commands_in_order()
    {
        var scene = new EditorScene();
        var stack = new EditorCommandStack(scene);

        stack.Execute(Place(scene, "a"));
        stack.Execute(Place(scene, "b"));

        stack.History.Should().HaveCount(2);
        stack.History[0].Should().Contain("a");
        stack.History[1].Should().Contain("b");
    }

    [Fact]
    public void Undo_and_redo_on_empty_history_return_false()
    {
        var stack = new EditorCommandStack(new EditorScene());

        stack.Undo().Should().BeFalse();
        stack.Redo().Should().BeFalse();
    }
}
