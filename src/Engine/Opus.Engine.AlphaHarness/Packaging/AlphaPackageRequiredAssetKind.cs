namespace Opus.Engine.AlphaHarness.Packaging;

/// <summary>Asset families an alpha package must declare in its manifest. The checklist
/// folds the per-format strings from <c>PackageAssetTypes</c> into one coarser-grained
/// family so the policy is independent of the specific file format used (glb vs gltf,
/// png vs ktx, etc.).</summary>
public enum AlphaPackageRequiredAssetKind
{
    /// <summary>At least one glTF/GLB model.</summary>
    Model,

    /// <summary>At least one PNG/JPEG/KTX texture.</summary>
    Texture,

    /// <summary>At least one font face/collection.</summary>
    Font,

    /// <summary>At least one JSON/CSV localisation table.</summary>
    Localisation,
}
