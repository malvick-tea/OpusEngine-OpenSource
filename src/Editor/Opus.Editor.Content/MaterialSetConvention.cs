using System;
using System.Collections.Generic;
using System.IO;

namespace Opus.Editor.Content;

/// <summary>
/// The on-disk authoring convention for a PBR material set, mirrored from the runtime's
/// <c>ExternalMaterialAtlasPlan</c> so the editor validates exactly the files the engine will load:
/// <c>{root}/{materialName}/{materialName}_{map}.png</c> with <c>map</c> in
/// <c>basecolor / normal / orm / emissive</c>. Pure path arithmetic; no IO. Kept in sync with the
/// renderer constants by intent — if the runtime convention changes, this mirror must change with it.
/// </summary>
public static class MaterialSetConvention
{
    public const string BaseColorToken = "basecolor";
    public const string NormalToken = "normal";
    public const string OrmToken = "orm";
    public const string EmissiveToken = "emissive";
    public const string MapExtension = ".png";

    /// <summary>The four authored maps, in the canonical display order base / normal / orm / emissive.</summary>
    public static readonly IReadOnlyList<MaterialMapKind> AllKinds = new[]
    {
        MaterialMapKind.BaseColor,
        MaterialMapKind.Normal,
        MaterialMapKind.Orm,
        MaterialMapKind.Emissive,
    };

    /// <summary>The filename token a map kind uses, e.g. <c>basecolor</c>.</summary>
    public static string Token(MaterialMapKind kind) => kind switch
    {
        MaterialMapKind.BaseColor => BaseColorToken,
        MaterialMapKind.Normal => NormalToken,
        MaterialMapKind.Orm => OrmToken,
        MaterialMapKind.Emissive => EmissiveToken,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown material map kind."),
    };

    /// <summary>The path a material's map is expected at, relative to the textures root.</summary>
    public static string RelativeMapPath(string materialName, MaterialMapKind kind)
        => Path.Combine(materialName, $"{materialName}_{Token(kind)}{MapExtension}");

    /// <summary>The absolute path a material's map is expected at under the given textures root.</summary>
    public static string MapPath(string texturesRoot, string materialName, MaterialMapKind kind)
        => Path.Combine(texturesRoot, RelativeMapPath(materialName, kind));
}
