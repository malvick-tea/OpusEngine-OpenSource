namespace Opus.Engine.Consumer.Assets;

/// <summary>
/// Engine-neutral asset-resolution request sent from a host to a consumer catalog.
/// </summary>
/// <param name="Role">Host role the asset will fill.</param>
/// <param name="AssetId">Optional scene asset id associated with the request.</param>
public sealed record ConsumerAssetRequest(
    ConsumerAssetRole Role,
    ConsumerAssetId? AssetId);
