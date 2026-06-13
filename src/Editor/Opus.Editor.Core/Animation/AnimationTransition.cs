namespace Opus.Editor.Core;

/// <summary>
/// A directed edge between two animation states: when <see cref="Trigger"/> fires while
/// <see cref="From"/> is active, the runtime cross-fades to <see cref="To"/> over
/// <see cref="BlendSeconds"/>. The trigger is an opaque engine-neutral name the consumer's runtime raises
/// (no game rule is modelled here — the editor only wires the graph). States are referenced by id so a
/// rename never breaks the edge. Immutable; mutations go through graph commands.
/// </summary>
/// <param name="From">Source state id.</param>
/// <param name="To">Destination state id.</param>
/// <param name="Trigger">Named trigger that fires the transition.</param>
/// <param name="BlendSeconds">Cross-fade duration in seconds (0 = a hard cut).</param>
public sealed record AnimationTransition(
    AnimationStateId From, AnimationStateId To, string Trigger, float BlendSeconds);
