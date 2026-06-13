using System;
using System.Collections.Generic;

namespace Opus.Editor.Core;

/// <summary>
/// The mutable element graph behind one open document: the placed nodes and the scene lights, each in its
/// own densely-id-allocated <see cref="SceneElementStore{TId,TElement}"/> (so the two kinds share one
/// allocation / lookup mechanism instead of duplicating it). <see cref="ISceneCommand"/> implementations are
/// the only writers, so every change is undoable and the pseudo-code mirror always reads a consistent
/// snapshot. Pure and GPU-free.
/// </summary>
public sealed class EditorScene
{
    public const string DefaultName = "untitled";

    private readonly SceneElementStore<SceneNodeId, SceneNode> _nodes =
        new(static node => node.Id, static value => new SceneNodeId(value), "Node");

    private readonly SceneElementStore<SceneLightId, SceneLight> _lights =
        new(static light => light.Id, static value => new SceneLightId(value), "Light");

    public string Name { get; set; } = DefaultName;

    public IReadOnlyList<SceneNode> Nodes => _nodes.Elements;

    public int Count => _nodes.Count;

    public IReadOnlyList<SceneLight> Lights => _lights.Elements;

    public int LightCount => _lights.Count;

    /// <summary>Allocates the next dense node id without inserting a node.</summary>
    public SceneNodeId AllocateId() => _nodes.AllocateId();

    public bool Contains(SceneNodeId id) => _nodes.Contains(id);

    public int IndexOf(SceneNodeId id) => _nodes.IndexOf(id);

    public SceneNode? Find(SceneNodeId id) => _nodes.Find(id);

    public void Add(SceneNode node) => _nodes.Add(node);

    public void Insert(int index, SceneNode node) => _nodes.Insert(index, node);

    public SceneNode RemoveAt(int index) => _nodes.RemoveAt(index);

    public void Replace(SceneNode node) => _nodes.Replace(node);

    /// <summary>Allocates the next dense light id, from a sequence independent of the node ids.</summary>
    public SceneLightId AllocateLightId() => _lights.AllocateId();

    public bool ContainsLight(SceneLightId id) => _lights.Contains(id);

    public int IndexOfLight(SceneLightId id) => _lights.IndexOf(id);

    public SceneLight? FindLight(SceneLightId id) => _lights.Find(id);

    public void AddLight(SceneLight light) => _lights.Add(light);

    public void InsertLight(int index, SceneLight light) => _lights.Insert(index, light);

    public SceneLight RemoveLightAt(int index) => _lights.RemoveAt(index);

    public void ReplaceLight(SceneLight light) => _lights.Replace(light);

    public EditorSceneDocument Snapshot() =>
        new(Name, new List<SceneNode>(_nodes.Elements)) { Lights = new List<SceneLight>(_lights.Elements) };

    public void Load(EditorSceneDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _nodes.LoadFrom(document.Nodes);
        _lights.LoadFrom(document.Lights);
        Name = document.Name;
    }
}
