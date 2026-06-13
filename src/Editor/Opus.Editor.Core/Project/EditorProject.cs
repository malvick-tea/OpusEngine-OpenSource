using System;
using System.Collections.Generic;

namespace Opus.Editor.Core;

/// <summary>
/// The mutable manifest behind one open editor project. Owns four de-duplicated path lists — content
/// roots, scenes, animation graphs, material roots — added and removed through coarse, file-level
/// operations (a project is configuration, not an interactive document, so it has no undo stack; the host
/// loads, mutates, and saves). Pure and GPU-free.
/// </summary>
public sealed class EditorProject
{
    public const string DefaultName = "untitled";

    private readonly List<string> _contentRoots = new();
    private readonly List<string> _scenes = new();
    private readonly List<string> _animationGraphs = new();
    private readonly List<string> _materialRoots = new();

    public string Name { get; set; } = DefaultName;

    public IReadOnlyList<string> ContentRoots => _contentRoots;

    public IReadOnlyList<string> Scenes => _scenes;

    public IReadOnlyList<string> AnimationGraphs => _animationGraphs;

    public IReadOnlyList<string> MaterialRoots => _materialRoots;

    public bool AddContentRoot(string path) => AddUnique(_contentRoots, path);

    public bool AddScene(string path) => AddUnique(_scenes, path);

    public bool AddAnimationGraph(string path) => AddUnique(_animationGraphs, path);

    public bool AddMaterialRoot(string path) => AddUnique(_materialRoots, path);

    public bool RemoveContentRoot(string path) => _contentRoots.Remove(path);

    public bool RemoveScene(string path) => _scenes.Remove(path);

    public bool RemoveAnimationGraph(string path) => _animationGraphs.Remove(path);

    public bool RemoveMaterialRoot(string path) => _materialRoots.Remove(path);

    public EditorProjectDocument Snapshot() => new(
        Name,
        new List<string>(_contentRoots),
        new List<string>(_scenes),
        new List<string>(_animationGraphs),
        new List<string>(_materialRoots));

    public void Load(EditorProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        Name = document.Name;
        Replace(_contentRoots, document.ContentRoots);
        Replace(_scenes, document.Scenes);
        Replace(_animationGraphs, document.AnimationGraphs);
        Replace(_materialRoots, document.MaterialRoots);
    }

    private static bool AddUnique(List<string> list, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (list.Contains(path, StringComparer.Ordinal))
        {
            return false;
        }

        list.Add(path);
        return true;
    }

    private static void Replace(List<string> list, IReadOnlyList<string> values)
    {
        list.Clear();
        list.AddRange(values);
    }
}
