namespace Opus.Content.Packaging.Manifest;

/// <summary>
/// Stable identifiers for optional package features the M6 validator understands.
/// Packages opt into feature gates by listing them in
/// <see cref="ContentPackageManifest.RequiredFeatures"/>; unknown identifiers fail
/// validation rather than being silently ignored, because a required feature the
/// validator does not understand may have content the runtime cannot honour.
/// <para>
/// Append-only. Removing a feature is a breaking schema change and must move to a
/// new manifest <see cref="ManifestFormatVersion"/> major.
/// </para>
/// </summary>
public static class PackageFeatures
{
    /// <summary>Package contains glTF/GLB models.</summary>
    public const string Models = "models";

    /// <summary>Package contains PNG/JPEG/KTX textures.</summary>
    public const string Textures = "textures";

    /// <summary>Package contains TrueType/OpenType/Collection font files.</summary>
    public const string Fonts = "fonts";

    /// <summary>Package contains localisation key/value tables (JSON or CSV).</summary>
    public const string Localisation = "localisation";

    private static readonly HashSet<string> SupportedSet = new(StringComparer.Ordinal)
    {
        Models,
        Textures,
        Fonts,
        Localisation,
    };

    /// <summary>Returns the immutable set of feature identifiers supported by this
    /// validator. Membership checks use <see cref="StringComparer.Ordinal"/>.</summary>
    public static IReadOnlySet<string> All => SupportedSet;

    /// <summary>Returns true when <paramref name="feature"/> is a known package feature.</summary>
    public static bool IsSupported(string feature) => SupportedSet.Contains(feature);
}
