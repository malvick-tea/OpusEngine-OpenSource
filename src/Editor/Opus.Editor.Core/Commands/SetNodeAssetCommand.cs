using System;

namespace Opus.Editor.Core;

/// <summary>
/// Replaces a node's asset reference (null clears it back to an empty grouping node), capturing the
/// previous reference on the first apply so <see cref="Revert"/> (and a later redo) round-trips exactly.
/// </summary>
public sealed class SetNodeAssetCommand : ISceneCommand
{
    private readonly SceneNodeId _id;
    private readonly string? _next;
    private string? _previous;
    private bool _captured;

    public SetNodeAssetCommand(SceneNodeId id, string? next)
    {
        _id = id;
        _next = next;
    }

    public void Apply(EditorScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var node = Require(scene);
        if (!_captured)
        {
            _previous = node.AssetRef;
            _captured = true;
        }

        scene.Replace(node.WithAssetRef(_next));
    }

    public void Revert(EditorScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var node = Require(scene);
        scene.Replace(node.WithAssetRef(_previous));
    }

    public string Describe() => $"set asset of #{_id} to \"{_next ?? "-"}\"";

    private SceneNode Require(EditorScene scene) => scene.Find(_id)
        ?? throw new InvalidOperationException($"Cannot set the asset of node #{_id}: not present.");
}
