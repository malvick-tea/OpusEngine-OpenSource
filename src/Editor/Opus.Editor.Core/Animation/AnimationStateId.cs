using System.Globalization;

namespace Opus.Editor.Core;

/// <summary>
/// Stable identity of an <see cref="AnimationState"/> within one animation graph. Allocated densely and
/// deterministically by <see cref="AnimationGraph"/> so a replayed command sequence reproduces identical
/// ids — the invariant transitions, the pseudo-code mirror, and undo / redo all rely on (a transition
/// references states by id, so renaming a state never breaks an edge).
/// </summary>
/// <param name="Value">The 1-based dense id. Zero is reserved for <see cref="None"/>.</param>
public readonly record struct AnimationStateId(int Value)
{
    /// <summary>The unset id — marks "no entry state" and the like; a real state never carries it.</summary>
    public static readonly AnimationStateId None = new(0);

    public bool IsValid => Value > 0;

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
