using Opus.Foundation.Geometry;

namespace Opus.Editor.Ui;

/// <summary>
/// Supplies a model's local-space bounds for an asset reference so the viewport can build pick boxes for
/// placed nodes. The host backs this with model inspection; a null result means the bounds are unknown
/// and the picker falls back to a small default box.
/// </summary>
public interface IModelBoundsSource
{
    Aabb? TryGetLocalBounds(string assetRef);
}
