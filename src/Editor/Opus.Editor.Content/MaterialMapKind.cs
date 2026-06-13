namespace Opus.Editor.Content;

/// <summary>
/// The four authored PBR maps the editor validates on disk, matching the runtime's loose-file material
/// convention (see <c>ExternalMaterialAtlasPlan</c> in the D3D12 renderer): base colour, tangent-space
/// normal, the packed ORM (R = occlusion, G = roughness, B = metallic), and emissive. The ORM map feeds
/// both the metallic-roughness and occlusion shader slots at runtime. A missing map is not an error — the
/// GPU layer substitutes a neutral fallback — so an incomplete set still renders, just flat.
/// </summary>
public enum MaterialMapKind
{
    BaseColor,
    Normal,
    Orm,
    Emissive,
}
