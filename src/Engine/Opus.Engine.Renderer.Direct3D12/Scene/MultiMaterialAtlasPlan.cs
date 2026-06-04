using System.Collections.Generic;
using Opus.Content.Meshes;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>Pure-CPU planning step for <see cref="MultiMaterialAtlas"/>. Given a list of
/// per-material bindings from <see cref="GltfImageReader.ReadMaterialBindings"/>, computes:
/// <list type="bullet">
/// <item><description>The ordered list of distinct embedded-image indices the GPU layer
/// must decode + upload (so N materials referencing K&lt;=N distinct images stay
/// proportional to K, not N).</description></item>
/// <item><description>The per-material <see cref="MultiMaterialSlot"/> table: for each
/// material, which unique-image slot the renderer binds (or fallback when the binding
/// can't be resolved).</description></item>
/// </list>
/// Lives behind an <see cref="MultiMaterialAtlas"/> wall so GPU-free regression tests can
/// pin the dedup + slot-mapping arithmetic without standing up a D3D12 device.</summary>
public static class MultiMaterialAtlasPlan
{
    public static MultiMaterialAtlasLayout Build(IReadOnlyList<GltfMaterialBinding> bindings)
    {
        if (bindings.Count == 0)
        {
            return EmptyLayout;
        }

        var imageToSlot = new Dictionary<int, int>(bindings.Count);
        var uniqueIndices = new List<int>(bindings.Count);
        var slots = new MultiMaterialSlot[bindings.Count];

        // First reference to a glTF image (by ANY map of ANY material) claims the next unique
        // slot; later references — including a different map kind — reuse it. So the GPU layer
        // uploads each distinct image exactly once even though a material binds up to five maps.
        int? SlotFor(int? imageIndex)
        {
            if (imageIndex is not int idx)
            {
                return null;
            }

            if (!imageToSlot.TryGetValue(idx, out var existing))
            {
                existing = uniqueIndices.Count;
                imageToSlot[idx] = existing;
                uniqueIndices.Add(idx);
            }

            return existing;
        }

        for (var m = 0; m < bindings.Count; m++)
        {
            var binding = bindings[m];
            slots[m] = new MultiMaterialSlot(SlotFor(binding.BaseColorImageIndex), binding.BaseColorFactor)
            {
                NormalSlot = SlotFor(binding.NormalImageIndex),
                MetallicRoughnessSlot = SlotFor(binding.MetallicRoughnessImageIndex),
                OcclusionSlot = SlotFor(binding.OcclusionImageIndex),
                EmissiveSlot = SlotFor(binding.EmissiveImageIndex),
                MetallicFactor = binding.MetallicFactor,
                RoughnessFactor = binding.RoughnessFactor,
                EmissiveFactor = binding.EmissiveFactor,
            };
        }

        return new MultiMaterialAtlasLayout(uniqueIndices, slots);
    }

    private static readonly MultiMaterialAtlasLayout EmptyLayout = new(
        new int[0],
        new MultiMaterialSlot[0]);
}
