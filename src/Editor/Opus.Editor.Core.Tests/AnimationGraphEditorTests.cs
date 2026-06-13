using FluentAssertions;
using Xunit;

namespace Opus.Editor.Core.Tests;

public sealed class AnimationGraphEditorTests
{
    [Fact]
    public void Add_state_marks_dirty_and_returns_an_id()
    {
        var editor = new AnimationGraphEditor("locomotion");

        var id = editor.AddState("Idle", "idle.glb");

        id.IsValid.Should().BeTrue();
        editor.IsDirty.Should().BeTrue();
        editor.Graph.FindState(id)!.ClipRef.Should().Be("idle.glb");
    }

    [Fact]
    public void Transition_to_a_missing_state_is_rejected()
    {
        var editor = new AnimationGraphEditor();
        var idle = editor.AddState("Idle");

        editor.AddTransition(idle, new AnimationStateId(99), "move").Should().BeFalse();
        editor.Graph.TransitionCount.Should().Be(0);
    }

    [Fact]
    public void Transition_between_existing_states_is_wired()
    {
        var editor = new AnimationGraphEditor();
        var idle = editor.AddState("Idle");
        var walk = editor.AddState("Walk");

        editor.AddTransition(idle, walk, "move", 0.2f).Should().BeTrue();

        editor.Graph.Transitions[0].Should().Be(new AnimationTransition(idle, walk, "move", 0.2f));
    }

    [Fact]
    public void Set_entry_rejects_a_missing_state_but_accepts_none()
    {
        var editor = new AnimationGraphEditor();
        var idle = editor.AddState("Idle");

        editor.SetEntryState(new AnimationStateId(42)).Should().BeFalse();
        editor.SetEntryState(idle).Should().BeTrue();
        editor.EntryState.Should().Be(idle);
        editor.SetEntryState(AnimationStateId.None).Should().BeTrue();
        editor.EntryState.Should().Be(AnimationStateId.None);
    }

    [Fact]
    public void Rename_then_undo_restores_the_previous_name()
    {
        var editor = new AnimationGraphEditor();
        var idle = editor.AddState("Idle");

        editor.RenameState(idle, "Stand");
        editor.Graph.FindState(idle)!.Name.Should().Be("Stand");

        editor.Undo();
        editor.Graph.FindState(idle)!.Name.Should().Be("Idle");
    }

    [Fact]
    public void Bind_clip_then_undo_restores_the_previous_clip()
    {
        var editor = new AnimationGraphEditor();
        var idle = editor.AddState("Idle");

        editor.BindClip(idle, "idle.glb");
        editor.Graph.FindState(idle)!.ClipRef.Should().Be("idle.glb");

        editor.Undo();
        editor.Graph.FindState(idle)!.ClipRef.Should().BeNull();
    }

    [Fact]
    public void Removing_a_state_cascades_to_its_transitions()
    {
        var editor = new AnimationGraphEditor();
        var idle = editor.AddState("Idle");
        var walk = editor.AddState("Walk");
        editor.AddTransition(idle, walk, "move");
        editor.AddTransition(walk, idle, "stop");

        editor.RemoveState(walk).Should().BeTrue();

        editor.Graph.TransitionCount.Should().Be(0);
        editor.Graph.StateCount.Should().Be(1);
    }

    [Fact]
    public void Undo_of_remove_restores_state_transitions_and_entry()
    {
        var editor = new AnimationGraphEditor();
        var idle = editor.AddState("Idle");
        var walk = editor.AddState("Walk");
        editor.AddTransition(idle, walk, "move");
        editor.SetEntryState(walk);

        editor.RemoveState(walk);
        editor.EntryState.Should().Be(AnimationStateId.None);

        editor.Undo();

        editor.Graph.StateCount.Should().Be(2);
        editor.Graph.TransitionCount.Should().Be(1);
        editor.EntryState.Should().Be(walk);
    }

    [Fact]
    public void Remove_transition_drops_only_the_matching_edge()
    {
        var editor = new AnimationGraphEditor();
        var idle = editor.AddState("Idle");
        var walk = editor.AddState("Walk");
        editor.AddTransition(idle, walk, "move");
        editor.AddTransition(walk, idle, "stop");

        editor.RemoveTransition(idle, walk, "move").Should().BeTrue();

        editor.Graph.TransitionCount.Should().Be(1);
        editor.Graph.Transitions[0].Should().Be(new AnimationTransition(walk, idle, "stop", 0f));
        editor.Graph.StateCount.Should().Be(2);
    }

    [Fact]
    public void Remove_transition_undo_restores_the_edge_at_its_position()
    {
        var editor = new AnimationGraphEditor();
        var idle = editor.AddState("Idle");
        var walk = editor.AddState("Walk");
        editor.AddTransition(idle, walk, "move");
        editor.AddTransition(walk, idle, "stop");

        editor.RemoveTransition(idle, walk, "move");
        editor.Undo();

        editor.Graph.TransitionCount.Should().Be(2);
        editor.Graph.Transitions[0].Should().Be(new AnimationTransition(idle, walk, "move", 0f));
    }

    [Fact]
    public void Removing_a_missing_transition_is_rejected()
    {
        var editor = new AnimationGraphEditor();
        var idle = editor.AddState("Idle");
        var walk = editor.AddState("Walk");

        editor.RemoveTransition(idle, walk, "move").Should().BeFalse();
    }

    [Fact]
    public void State_id_is_stable_across_undo_and_redo()
    {
        var editor = new AnimationGraphEditor();
        var idle = editor.AddState("Idle");

        editor.Undo();
        editor.Graph.StateCount.Should().Be(0);

        editor.Redo();
        editor.Graph.States[0].Id.Should().Be(idle);
    }

    [Fact]
    public void Allocation_after_undo_never_reuses_an_id()
    {
        var editor = new AnimationGraphEditor();
        editor.AddState("A");
        var b = editor.AddState("B");

        editor.Undo();
        var c = editor.AddState("C");

        c.Value.Should().BeGreaterThan(b.Value);
        editor.Graph.StateCount.Should().Be(2);
    }

    [Fact]
    public void Changed_event_fires_on_mutation()
    {
        var editor = new AnimationGraphEditor();
        int notifications = 0;
        editor.Changed += () => notifications++;

        editor.AddState("Idle");

        notifications.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Load_graph_replaces_document_and_resets_history()
    {
        var editor = new AnimationGraphEditor("scratch");
        editor.AddState("Temp");

        var loaded = new AnimationGraphDocument(
            "Locomotion",
            new AnimationStateId(3),
            new[] { new AnimationState(new AnimationStateId(3), "Idle", "idle.glb", true, 1f) },
            System.Array.Empty<AnimationTransition>());
        editor.LoadGraph(loaded);

        editor.Name.Should().Be("Locomotion");
        editor.Graph.StateCount.Should().Be(1);
        editor.IsDirty.Should().BeFalse();
        editor.CanUndo.Should().BeFalse();
        editor.AddState("Next").Value.Should().Be(4);
    }
}
