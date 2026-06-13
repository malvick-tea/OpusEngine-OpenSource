using System.Globalization;

namespace Opus.Editor.Core;

/// <summary>
/// Stable identity of a <see cref="SceneLight"/> within one document. Allocated densely and
/// deterministically by <see cref="EditorScene"/> from a sequence independent of the node ids, so a
/// replayed command sequence reproduces identical light ids — the invariant the pseudo-code mirror and the
/// undo / redo stack both rely on.
/// </summary>
/// <param name="Value">The 1-based dense id. Zero is reserved for <see cref="None"/>.</param>
public readonly record struct SceneLightId(int Value) : ISceneElementId
{
    /// <summary>The unset id. A real light never carries this; it marks "no light" (an unallocated spec).</summary>
    public static readonly SceneLightId None = new(0);

    public bool IsValid => Value > 0;

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
