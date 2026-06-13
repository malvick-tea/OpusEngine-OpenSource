using System;
using System.Collections.Generic;

namespace Opus.Editor.Content;

/// <summary>
/// Validates a PBR material set against the on-disk authoring convention: for one material name it probes
/// each of the four expected maps and reports which are present. Pure — the caller injects the file-exists
/// probe (which owns IO), mirroring <c>ExternalMaterialAtlasPlan</c> so the validation matches exactly what
/// the runtime will load.
/// </summary>
public static class MaterialSetInspector
{
    public static MaterialSetReport Inspect(string texturesRoot, string materialName, Func<string, bool> fileExists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(texturesRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(materialName);
        ArgumentNullException.ThrowIfNull(fileExists);

        var maps = new List<MaterialMapStatus>(MaterialSetConvention.AllKinds.Count);
        int presentCount = 0;
        bool hasBaseColor = false;
        foreach (var kind in MaterialSetConvention.AllKinds)
        {
            string absolute = MaterialSetConvention.MapPath(texturesRoot, materialName, kind);
            string relative = MaterialSetConvention.RelativeMapPath(materialName, kind);
            bool present = fileExists(absolute);
            maps.Add(new MaterialMapStatus(kind, relative, present));
            if (present)
            {
                presentCount++;
                hasBaseColor |= kind == MaterialMapKind.BaseColor;
            }
        }

        return new MaterialSetReport(materialName, maps, presentCount, hasBaseColor);
    }
}
