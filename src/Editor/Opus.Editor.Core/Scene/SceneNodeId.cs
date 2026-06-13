using System.Globalization;

namespace Opus.Editor.Core;

/// <summary>
/// Stable identity of a <see cref="SceneNode"/> within one document. Allocated densely and
/// deterministically by <see cref="EditorScene"/>, so a replayed command sequence reproduces identical
/// ids — the invariant the pseudo-code mirror and the undo / redo stack both rely on.
/// </summary>
/// <param name="Value">The 1-based dense id. Zero is reserved for <see cref="None"/>.</param>
public readonly record struct SceneNodeId(int Value) : ISceneElementId
{
    /// <summary>The unset id. A real node never carries this; it marks "no selection" and the like.</summary>
    public static readonly SceneNodeId None = new(0);

    public bool IsValid => Value > 0;

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
