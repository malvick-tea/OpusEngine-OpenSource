namespace Opus.Engine.Consumer.Assets;

/// <summary>
/// Engine-neutral resolver for consumer-owned assets. Hosts query this instead of
/// hard-wiring a single backend option such as an alpha sample asset path.
/// </summary>
public interface IConsumerAssetCatalog
{
    /// <summary>Resolves an asset request into a filesystem path or an unresolved result.</summary>
    ConsumerAssetResolution ResolveAsset(ConsumerAssetRequest request);
}
