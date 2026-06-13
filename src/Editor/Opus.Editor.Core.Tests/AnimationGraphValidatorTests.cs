using System;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Opus.Editor.Core.Tests;

public sealed class AnimationGraphValidatorTests
{
    private static AnimationState State(int id, string name) =>
        new(new AnimationStateId(id), name, null, true, 1f);

    [Fact]
    public void Empty_graph_is_valid()
    {
        var document = AnimationGraphDocument.Empty("blank");

        AnimationGraphValidator.IsValid(document).Should().BeTrue();
    }

    [Fact]
    public void A_well_formed_graph_has_no_issues()
    {
        var document = new AnimationGraphDocument(
            "loco",
            new AnimationStateId(1),
            new[] { State(1, "Idle"), State(2, "Walk") },
            new[] { new AnimationTransition(new AnimationStateId(1), new AnimationStateId(2), "move", 0f) });

        AnimationGraphValidator.Validate(document).Should().BeEmpty();
    }

    [Fact]
    public void Duplicate_state_names_are_flagged()
    {
        var document = new AnimationGraphDocument(
            "loco",
            new AnimationStateId(1),
            new[] { State(1, "Idle"), State(2, "Idle") },
            Array.Empty<AnimationTransition>());

        AnimationGraphValidator.Validate(document)
            .Should().Contain(issue => issue.Kind == AnimationGraphIssueKind.DuplicateStateName);
    }

    [Fact]
    public void States_without_an_entry_are_flagged()
    {
        var document = new AnimationGraphDocument(
            "loco",
            AnimationStateId.None,
            new[] { State(1, "Idle") },
            Array.Empty<AnimationTransition>());

        AnimationGraphValidator.Validate(document)
            .Should().Contain(issue => issue.Kind == AnimationGraphIssueKind.MissingEntryState);
    }

    [Fact]
    public void An_unresolved_entry_state_is_flagged()
    {
        var document = new AnimationGraphDocument(
            "loco",
            new AnimationStateId(99),
            new[] { State(1, "Idle") },
            Array.Empty<AnimationTransition>());

        AnimationGraphValidator.Validate(document)
            .Should().Contain(issue => issue.Kind == AnimationGraphIssueKind.EntryStateNotFound);
    }

    [Fact]
    public void Transitions_to_or_from_missing_states_are_flagged()
    {
        var document = new AnimationGraphDocument(
            "loco",
            new AnimationStateId(1),
            new[] { State(1, "Idle") },
            new[] { new AnimationTransition(new AnimationStateId(1), new AnimationStateId(8), "move", 0f) });

        var kinds = AnimationGraphValidator.Validate(document).Select(issue => issue.Kind).ToArray();

        kinds.Should().Contain(AnimationGraphIssueKind.TransitionToMissing);
    }
}
