using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Opus.Editor.Core;

/// <summary>
/// Projects an <see cref="AnimationGraphDocument"/> into the Opus editor's readable pseudo-code: a clean,
/// declarative, engine-neutral mirror of the state graph that updates live as the document changes. State
/// ids are resolved to names for the entry declaration and each transition, so the text reads like the
/// graph a developer would hand-write. A pure function of the document — identical input yields identical
/// text, with '\n' line endings — so it is fully unit-testable and the reliable "what the runtime will
/// drive" view.
/// </summary>
public static class AnimationGraphDslWriter
{
    public static string Write(AnimationGraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var names = BuildNameLookup(document.States);
        var sb = new StringBuilder();
        sb.Append("animgraph ").Append(DslText.Quote(document.Name)).Append(" {").Append(DslText.Newline);
        if (document.EntryState.IsValid)
        {
            DslText.Line(sb, 1, "entry " + Reference(document.EntryState, names));
        }

        foreach (var state in document.States)
        {
            WriteState(sb, state);
        }

        foreach (var transition in document.Transitions)
        {
            WriteTransition(sb, transition, names);
        }

        sb.Append('}').Append(DslText.Newline);
        return sb.ToString();
    }

    private static void WriteState(StringBuilder sb, AnimationState state)
    {
        DslText.Line(sb, 1, "state " + DslText.Quote(state.Name) + " {");
        DslText.Line(sb, 2, "id " + state.Id.Value.ToString(CultureInfo.InvariantCulture));
        if (state.ClipRef is not null)
        {
            DslText.Line(sb, 2, "clip " + DslText.Quote(state.ClipRef));
        }

        DslText.Line(sb, 2, "loop " + (state.Loop ? "true" : "false"));
        DslText.Line(sb, 2, "speed " + DslText.Num(state.Speed));
        DslText.Line(sb, 1, "}");
    }

    private static void WriteTransition(
        StringBuilder sb, AnimationTransition transition, IReadOnlyDictionary<int, string> names)
    {
        string from = Reference(transition.From, names);
        string to = Reference(transition.To, names);
        string text = "transition " + from + " -> " + to +
            " on " + DslText.Quote(transition.Trigger) + " blend " + DslText.Num(transition.BlendSeconds);
        DslText.Line(sb, 1, text);
    }

    private static IReadOnlyDictionary<int, string> BuildNameLookup(IReadOnlyList<AnimationState> states)
    {
        var map = new Dictionary<int, string>(states.Count);
        foreach (var state in states)
        {
            map[state.Id.Value] = state.Name;
        }

        return map;
    }

    private static string Reference(AnimationStateId id, IReadOnlyDictionary<int, string> names) =>
        names.TryGetValue(id.Value, out string? name)
            ? DslText.Quote(name)
            : "#" + id.Value.ToString(CultureInfo.InvariantCulture);
}
