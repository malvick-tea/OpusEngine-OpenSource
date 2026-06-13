using System.Collections.Generic;
using System.Numerics;
using Opus.Editor.Core;

namespace Opus.Editor.Ui;

/// <summary>
/// The non-primary members of a multi-selection during a group drag. When a translate-capable drag begins,
/// the followers are snapshotted at their start transforms; each preview frame moves them by the primary's
/// world delta, and the drag-end commit collects each follower's net change. Only positions propagate —
/// rotate / scale drags keep their per-element pivots and capture no followers. Owned by the
/// <see cref="ViewportController"/> drag gestures so the multi-selection moves as one and commits as one
/// reversible edit; previews go through the document's no-undo preview path.
/// </summary>
internal sealed class SelectionFollowers
{
    private readonly EditorDocument _document;
    private readonly List<(SceneNodeId Id, EditorTransform Start)> _nodes = new();
    private readonly List<SceneLight> _lights = new();

    public SelectionFollowers(EditorDocument document)
    {
        _document = document;
    }

    /// <summary>Snapshots the selected members other than the dragged <paramref name="primary"/>, so a
    /// translate-capable drag moves the whole multi-selection by the primary's delta.</summary>
    public void Capture(SceneElementRef primary)
    {
        _nodes.Clear();
        _lights.Clear();
        foreach (var element in _document.SelectedElements)
        {
            if (element == primary)
            {
                continue;
            }

            if (element.IsNode && _document.Scene.Find(element.AsNode) is { } node)
            {
                _nodes.Add((node.Id, node.Transform));
            }
            else if (element.IsLight && _document.Scene.FindLight(element.AsLight) is { } light)
            {
                _lights.Add(light);
            }
        }
    }

    public void Clear()
    {
        _nodes.Clear();
        _lights.Clear();
    }

    /// <summary>Previews every follower at its start position plus the primary's world delta — the live half
    /// of a group drag, mirroring how the primary previews without an undo step.</summary>
    public void Preview(Vector3 delta)
    {
        foreach (var (id, start) in _nodes)
        {
            var moved = start with { Position = Float3.FromVector3(start.Position.ToVector3() + delta) };
            var current = _document.Scene.Find(id);
            if (current is not null && current.Transform != moved)
            {
                _document.PreviewNodeTransform(id, moved);
            }
        }

        foreach (var start in _lights)
        {
            var moved = start with { Position = Float3.FromVector3(start.Position.ToVector3() + delta) };
            if (_document.Scene.FindLight(start.Id) is { } current && current != moved)
            {
                _document.PreviewLight(moved);
            }
        }
    }

    /// <summary>Collects each follower's net change for the drag-end group commit.</summary>
    public void AppendMoves(List<NodeMove> nodes, List<LightMove> lights)
    {
        foreach (var (id, start) in _nodes)
        {
            if (_document.Scene.Find(id) is { } current && current.Transform != start)
            {
                nodes.Add(new NodeMove(id, start, current.Transform));
            }
        }

        foreach (var start in _lights)
        {
            if (_document.Scene.FindLight(start.Id) is { } current && current != start)
            {
                lights.Add(new LightMove(start, current));
            }
        }
    }
}
