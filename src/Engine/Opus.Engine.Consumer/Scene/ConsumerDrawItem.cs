using System.Numerics;
using Opus.Engine.Consumer.Assets;

namespace Opus.Engine.Consumer.Scene;

/// <summary>
/// One engine-neutral draw declaration emitted by a consumer scene source. The host maps
/// <see cref="AssetId"/> into backend resources and applies <see cref="World"/> plus
/// <see cref="TintFactor"/> without knowing what gameplay object produced the item.
/// </summary>
public readonly record struct ConsumerDrawItem(
    ConsumerAssetId AssetId,
    Matrix4x4 World,
    Vector4 TintFactor)
{
    /// <summary>Creates a primary-scene draw item with identity tint.</summary>
    public ConsumerDrawItem(Matrix4x4 world)
        : this(ConsumerAssetId.PrimarySceneModel, world, Vector4.One)
    {
    }
}
