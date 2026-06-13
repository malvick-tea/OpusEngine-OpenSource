namespace Opus.Editor.Core;

/// <summary>
/// One placed entry in an editor scene: an identity, a display name, an optional content asset reference
/// (a package-relative model path or asset id; null for an empty grouping node), and a local transform.
/// May carry a <see cref="ParentId"/> so nodes form a hierarchy (a grouping node parents its children).
/// Immutable — mutations go through <see cref="EditorScene"/> commands that replace the node, so undo /
/// redo and the pseudo-code mirror always observe a consistent snapshot.
/// </summary>
/// <param name="Id">Stable identity within the document.</param>
/// <param name="Name">Human-readable display name.</param>
/// <param name="AssetRef">Package-relative content reference, or null for a grouping node.</param>
/// <param name="Transform">Local transform (relative to the parent when the node is parented).</param>
public sealed record SceneNode(SceneNodeId Id, string Name, string? AssetRef, EditorTransform Transform)
{
    /// <summary>True when the node is hidden in the editor viewport (not drawn, not click-pickable; still
    /// listed in the outliner and the mirror). An init property rather than a constructor parameter so a
    /// pre-visibility scene file — no such JSON property — still loads with no schema bump.</summary>
    public bool Hidden { get; init; }

    /// <summary>The id of this node's parent, or null when the node is a root. The transform is then local
    /// to that parent (the render seam composes world transforms up the chain). An init property rather than
    /// a constructor parameter so a pre-hierarchy scene file — no such JSON property — still loads, as a
    /// root, with no schema bump (the same additive shape <see cref="Hidden"/> uses).</summary>
    public SceneNodeId? ParentId { get; init; }

    public SceneNode WithName(string name) => this with { Name = name };

    public SceneNode WithTransform(EditorTransform transform) => this with { Transform = transform };

    public SceneNode WithAssetRef(string? assetRef) => this with { AssetRef = assetRef };

    public SceneNode WithHidden(bool hidden) => this with { Hidden = hidden };

    public SceneNode WithParent(SceneNodeId? parentId) => this with { ParentId = parentId };
}
