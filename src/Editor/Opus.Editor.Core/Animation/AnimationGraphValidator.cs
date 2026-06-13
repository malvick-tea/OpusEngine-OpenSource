using System;
using System.Collections.Generic;

namespace Opus.Editor.Core;

/// <summary>
/// Pure validation of an <see cref="AnimationGraphDocument"/>: surfaces duplicate state names, an absent
/// or unresolved entry state, and transitions that reference states the graph does not contain. Returns
/// findings rather than throwing so the editor can show every problem at once and still let an
/// in-progress graph be saved. An empty graph (no states) is valid — it is simply not yet authored.
/// </summary>
public static class AnimationGraphValidator
{
    public static IReadOnlyList<AnimationGraphIssue> Validate(AnimationGraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var issues = new List<AnimationGraphIssue>();
        var ids = new HashSet<int>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var state in document.States)
        {
            ids.Add(state.Id.Value);
            if (!names.Add(state.Name))
            {
                issues.Add(new AnimationGraphIssue(
                    AnimationGraphIssueKind.DuplicateStateName, $"Duplicate state name '{state.Name}'."));
            }
        }

        ValidateEntry(document, ids, issues);
        ValidateTransitions(document, ids, issues);
        return issues;
    }

    public static bool IsValid(AnimationGraphDocument document) => Validate(document).Count == 0;

    private static void ValidateEntry(
        AnimationGraphDocument document, HashSet<int> ids, List<AnimationGraphIssue> issues)
    {
        if (document.States.Count == 0)
        {
            return;
        }

        if (!document.EntryState.IsValid)
        {
            issues.Add(new AnimationGraphIssue(
                AnimationGraphIssueKind.MissingEntryState, "Graph has states but no entry state."));
        }
        else if (!ids.Contains(document.EntryState.Value))
        {
            issues.Add(new AnimationGraphIssue(
                AnimationGraphIssueKind.EntryStateNotFound,
                $"Entry state #{document.EntryState} is not in the graph."));
        }
    }

    private static void ValidateTransitions(
        AnimationGraphDocument document, HashSet<int> ids, List<AnimationGraphIssue> issues)
    {
        foreach (var transition in document.Transitions)
        {
            if (!ids.Contains(transition.From.Value))
            {
                issues.Add(new AnimationGraphIssue(
                    AnimationGraphIssueKind.TransitionFromMissing,
                    $"Transition on '{transition.Trigger}' starts at missing state #{transition.From}."));
            }

            if (!ids.Contains(transition.To.Value))
            {
                issues.Add(new AnimationGraphIssue(
                    AnimationGraphIssueKind.TransitionToMissing,
                    $"Transition on '{transition.Trigger}' targets missing state #{transition.To}."));
            }
        }
    }
}
