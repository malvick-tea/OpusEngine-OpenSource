namespace Opus.Editor.Core;

/// <summary>One node's coalesced drag inside a group commit: undo restores <paramref name="From"/>, redo
/// reapplies <paramref name="To"/> — the per-element slice of a multi-selection drag.</summary>
/// <param name="Id">The dragged node.</param>
/// <param name="From">The transform the drag started from.</param>
/// <param name="To">The transform the drag ended on.</param>
public readonly record struct NodeMove(SceneNodeId Id, EditorTransform From, EditorTransform To);

/// <summary>One light's coalesced drag inside a group commit — the light twin of <see cref="NodeMove"/>
/// (the light's id rides inside the values).</summary>
/// <param name="From">The light value the drag started from.</param>
/// <param name="To">The light value the drag ended on.</param>
public readonly record struct LightMove(SceneLight From, SceneLight To);
