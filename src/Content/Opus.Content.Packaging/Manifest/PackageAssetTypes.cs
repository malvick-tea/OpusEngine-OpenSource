namespace Opus.Content.Packaging.Manifest;

/// <summary>
/// Stable manifest file-type identifiers understood by the Opus 0.1 package validator.
/// The manifest stores strings, but central constants keep code and tests searchable.
/// </summary>
public static class PackageAssetTypes
{
    /// <summary>glTF 2.0 binary model file.</summary>
    public const string ModelGlb = "model.glb";

    /// <summary>Split glTF 2.0 JSON model file, with sidecar buffers resolved by glTF URI.</summary>
    public const string ModelGltf = "model.gltf";

    /// <summary>PNG texture source file.</summary>
    public const string TexturePng = "texture.png";

    /// <summary>JPEG texture source file.</summary>
    public const string TextureJpeg = "texture.jpeg";

    /// <summary>KTX or KTX2 texture container. M6 validates headers only.</summary>
    public const string TextureKtx = "texture.ktx";

    /// <summary>TrueType/OpenType font face or collection.</summary>
    public const string Font = "font";

    /// <summary>JSON localisation key-value file.</summary>
    public const string LocalisationJson = "localisation.json";

    /// <summary>CSV localisation key-value file compatible with the current catalog shape.</summary>
    public const string LocalisationCsv = "localisation.csv";
}
