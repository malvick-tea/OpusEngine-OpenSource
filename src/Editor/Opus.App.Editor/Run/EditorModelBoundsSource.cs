using System;
using System.Collections.Generic;
using Opus.Editor.Ui;
using Opus.Foundation.Geometry;

namespace Opus.App.Editor.Run;

/// <summary>
/// Supplies each placed asset's local-space bounds to the editor viewport by inspecting the model under
/// the content roots once and caching the result (including misses). With several roots (a project
/// window) the first root that resolves the ref wins — the same precedence the place-model listing
/// implies. An asset that no root resolves returns null, so the picker falls back to its default box.
/// Host-side because it reads files; implements the GPU-free <see cref="IModelBoundsSource"/> the
/// viewport drives.
/// </summary>
public sealed class EditorModelBoundsSource : IModelBoundsSource
{
    private readonly IReadOnlyList<string> _contentRoots;
    private readonly Dictionary<string, Aabb?> _cache = new(StringComparer.Ordinal);

    public EditorModelBoundsSource(string contentRoot)
        : this(new[] { contentRoot })
    {
    }

    public EditorModelBoundsSource(IReadOnlyList<string> contentRoots)
    {
        ArgumentNullException.ThrowIfNull(contentRoots);
        foreach (string root in contentRoots)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(root, nameof(contentRoots));
        }

        _contentRoots = contentRoots;
    }

    public Aabb? TryGetLocalBounds(string assetRef)
    {
        if (string.IsNullOrWhiteSpace(assetRef))
        {
            return null;
        }

        if (_cache.TryGetValue(assetRef, out var cached))
        {
            return cached;
        }

        Aabb? bounds = null;
        foreach (string root in _contentRoots)
        {
            if (EditorModelResolver.InspectUnderRoot(root, assetRef) is { } inspection)
            {
                bounds = new Aabb(inspection.BoundsMin.ToVector3(), inspection.BoundsMax.ToVector3());
                break;
            }
        }

        _cache[assetRef] = bounds;
        return bounds;
    }
}
