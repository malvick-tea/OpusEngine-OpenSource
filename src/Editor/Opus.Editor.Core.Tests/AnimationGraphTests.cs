using System;
using FluentAssertions;
using Xunit;

namespace Opus.Editor.Core.Tests;

public sealed class AnimationGraphTests
{
    private static AnimationState State(AnimationGraph graph, string name) =>
        new(graph.AllocateId(), name, null, true, AnimationState.DefaultSpeed);

    [Fact]
    public void Allocated_ids_are_dense_and_unique()
    {
        var graph = new AnimationGraph();

        var a = graph.AllocateId();
        var b = graph.AllocateId();

        a.Value.Should().Be(1);
        b.Value.Should().Be(2);
    }

    [Fact]
    public void Add_then_find_returns_the_state()
    {
        var graph = new AnimationGraph();
        var state = State(graph, "Idle");
        graph.AddState(state);

        graph.FindState(state.Id).Should().Be(state);
        graph.FindStateByName("Idle").Should().Be(state);
        graph.StateCount.Should().Be(1);
    }

    [Fact]
    public void Adding_a_duplicate_id_throws()
    {
        var graph = new AnimationGraph();
        var state = State(graph, "Idle");
        graph.AddState(state);

        Action act = () => graph.AddState(state);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Remove_at_returns_the_removed_state()
    {
        var graph = new AnimationGraph();
        var state = State(graph, "Idle");
        graph.AddState(state);

        graph.RemoveStateAt(0).Should().Be(state);
        graph.StateCount.Should().Be(0);
    }

    [Fact]
    public void Transitions_preserve_insertion_order()
    {
        var graph = new AnimationGraph();
        var a = State(graph, "A");
        var b = State(graph, "B");
        graph.AddState(a);
        graph.AddState(b);
        graph.AddTransition(new AnimationTransition(a.Id, b.Id, "go", 0f));
        graph.AddTransition(new AnimationTransition(b.Id, a.Id, "back", 0f));

        graph.TransitionCount.Should().Be(2);
        graph.Transitions[0].Trigger.Should().Be("go");
        graph.Transitions[1].Trigger.Should().Be("back");
    }

    [Fact]
    public void Load_restores_a_document_and_continues_id_allocation()
    {
        var graph = new AnimationGraph();
        var document = new AnimationGraphDocument(
            "Locomotion",
            new AnimationStateId(7),
            new[] { new AnimationState(new AnimationStateId(7), "Idle", "idle.glb", true, 1f) },
            Array.Empty<AnimationTransition>());

        graph.Load(document);

        graph.Name.Should().Be("Locomotion");
        graph.EntryState.Should().Be(new AnimationStateId(7));
        graph.StateCount.Should().Be(1);
        graph.AllocateId().Value.Should().Be(8);
    }
}
