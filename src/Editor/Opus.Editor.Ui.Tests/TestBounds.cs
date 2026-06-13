using Opus.Foundation.Geometry;

namespace Opus.Editor.Ui.Tests;

/// <summary>A bounds source that returns the same box for every asset.</summary>
internal sealed class FixedBounds : IModelBoundsSource
{
    private readonly Aabb _box;

    public FixedBounds(Aabb box) => _box = box;

    public Aabb? TryGetLocalBounds(string assetRef) => _box;
}

/// <summary>A bounds source that never resolves, exercising the fallback-box path.</summary>
internal sealed class NullBounds : IModelBoundsSource
{
    public Aabb? TryGetLocalBounds(string assetRef) => null;
}
