namespace Opus.Editor.Core;

/// <summary>The kind of problem <see cref="AnimationGraphValidator"/> can find in a graph.</summary>
public enum AnimationGraphIssueKind
{
    /// <summary>Two or more states share a name; transitions and the runtime key on uniqueness.</summary>
    DuplicateStateName,

    /// <summary>The graph has states but declares no entry state, so the runtime cannot start it.</summary>
    MissingEntryState,

    /// <summary>The declared entry state id resolves to no state in the graph.</summary>
    EntryStateNotFound,

    /// <summary>A transition's source state id resolves to no state in the graph.</summary>
    TransitionFromMissing,

    /// <summary>A transition's destination state id resolves to no state in the graph.</summary>
    TransitionToMissing,
}

/// <summary>
/// One validation finding for an animation graph: the kind of problem and a human-readable message.
/// Read-only; produced by <see cref="AnimationGraphValidator"/>.
/// </summary>
/// <param name="Kind">The category of the problem.</param>
/// <param name="Message">A developer-facing description naming the offending state or transition.</param>
public sealed record AnimationGraphIssue(AnimationGraphIssueKind Kind, string Message);
