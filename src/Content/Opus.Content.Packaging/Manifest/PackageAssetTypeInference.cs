using System;
using System.IO;

namespace Opus.Content.Packaging.Manifest;

/// <summary>
/// Maps a content file name to the stable <see cref="PackageAssetTypes"/> identifier the
/// package validator understands, by file extension. A directory walk only has the
/// extension to go on, so an unrecognised extension returns <c>false</c> rather than
/// fabricating a type the validator would later reject. <c>.json</c> and <c>.csv</c> map to
/// localisation because those are the only text-content asset types in the Opus 0.1
/// validator vocabulary; an author with a non-localisation file of those extensions supplies
/// an explicit override at generation time.
/// </summary>
public static class PackageAssetTypeInference
{
    /// <summary>Tries to resolve the package asset type for <paramref name="fileName"/> from
    /// its extension. Returns false (with <paramref name="assetType"/> empty) when the
    /// extension is not one the M6 validator recognises.</summary>
    public static bool TryInferType(string fileName, out string assetType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        assetType = Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".glb" => PackageAssetTypes.ModelGlb,
            ".gltf" => PackageAssetTypes.ModelGltf,
            ".png" => PackageAssetTypes.TexturePng,
            ".jpg" or ".jpeg" => PackageAssetTypes.TextureJpeg,
            ".ktx" or ".ktx2" => PackageAssetTypes.TextureKtx,
            ".ttf" or ".otf" or ".ttc" => PackageAssetTypes.Font,
            ".json" => PackageAssetTypes.LocalisationJson,
            ".csv" => PackageAssetTypes.LocalisationCsv,
            _ => string.Empty,
        };
        return assetType.Length > 0;
    }
}
