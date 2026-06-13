using System.Collections.Generic;

namespace Opus.Editor.Core;

/// <summary>
/// The serialisable snapshot of an animation state graph: a display name, the entry state's id, and the
/// ordered state and transition lists. Engine-neutral orchestration content — generic states and edges,
/// no game concept — so it lives in the editor / engine tier, not a consumer repo. The on-disk schema
/// version is owned by the versioned envelope <c>AnimationGraphSerializer</c> writes around this payload,
/// so the record itself stays a clean data shape.
/// </summary>
/// <param name="Name">Display name of the graph.</param>
/// <param name="EntryState">Id of the state the graph starts in (<see cref="AnimationStateId.None"/> when
/// the graph has no states yet).</param>
/// <param name="States">Ordered state list; order is preserved across save / load and undo / redo.</param>
/// <param name="Transitions">Ordered transition list.</param>
public sealed record AnimationGraphDocument(
    string Name,
    AnimationStateId EntryState,
    IReadOnlyList<AnimationState> States,
    IReadOnlyList<AnimationTransition> Transitions)
{
    public static AnimationGraphDocument Empty(string name) => new(
        name, AnimationStateId.None, new List<AnimationState>(), new List<AnimationTransition>());
}
