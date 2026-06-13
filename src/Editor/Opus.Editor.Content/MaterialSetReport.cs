using System.Collections.Generic;

namespace Opus.Editor.Content;

/// <summary>
/// The validation result for one PBR material set: its name, the per-map presence, and the derived
/// completeness summary. Engine-neutral, read-only — the developer-facing "is this material fully
/// authored, and what will the runtime substitute?" view produced by <see cref="MaterialSetInspector"/>.
/// </summary>
/// <param name="MaterialName">The material set's folder name.</param>
/// <param name="Maps">Per-map presence, in <see cref="MaterialSetConvention.AllKinds"/> order.</param>
/// <param name="PresentCount">Number of authored maps present on disk (0..4).</param>
/// <param name="HasBaseColor">True when the base-colour map is present — the minimum for a material to
/// read as authored rather than a white fallback blob.</param>
public sealed record MaterialSetReport(
    string MaterialName,
    IReadOnlyList<MaterialMapStatus> Maps,
    int PresentCount,
    bool HasBaseColor)
{
    /// <summary>True when all four authored maps are present on disk.</summary>
    public bool IsComplete => PresentCount == MaterialSetConvention.AllKinds.Count;
}
