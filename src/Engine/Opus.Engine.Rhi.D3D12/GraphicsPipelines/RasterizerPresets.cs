using Silk.NET.Direct3D12;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Named <see cref="RasterizerDesc"/> presets used across the PSO factories.</summary>
internal static class RasterizerPresets
{
    /// <summary>Solid fill, no culling, default winding. Default for bufferless / fullscreen / particles.</summary>
    public static readonly RasterizerDesc SolidCullNone = new()
    {
        FillMode = FillMode.Solid,
        CullMode = CullMode.None,
        FrontCounterClockwise = 0,
        DepthBias = 0,
        DepthBiasClamp = 0f,
        SlopeScaledDepthBias = 0f,
        DepthClipEnable = 1,
        MultisampleEnable = 0,
        AntialiasedLineEnable = 0,
        ForcedSampleCount = 0,
        ConservativeRaster = ConservativeRasterizationMode.Off,
    };

    /// <summary>Solid fill, back-face cull, CCW front (System.Numerics convention). Default for lit meshes.</summary>
    public static readonly RasterizerDesc SolidCullBackCcw = new()
    {
        FillMode = FillMode.Solid,
        CullMode = CullMode.Back,
        FrontCounterClockwise = 1,
        DepthBias = 0,
        DepthBiasClamp = 0f,
        SlopeScaledDepthBias = 0f,
        DepthClipEnable = 1,
        MultisampleEnable = 0,
        AntialiasedLineEnable = 0,
        ForcedSampleCount = 0,
        ConservativeRaster = ConservativeRasterizationMode.Off,
    };

    /// <summary>Solid fill, NO culling, CCW front. Used by the glTF lit-mesh PSO because
    /// the Sketchfab spec-gloss exports we ship (Pz.IV.G and friends) declare every
    /// material <c>doubleSided: true</c> — a back-face cull on those primitives drops
    /// the inside-out side panels and hatches the artist authored with reversed winding.
    /// Lambert lighting in the scene PS naturally darkens back-facing pixels (NoL → 0),
    /// so disabled culling costs visual quality only on intentionally one-sided geometry,
    /// of which we ship none through this PSO.</summary>
    public static readonly RasterizerDesc SolidCullNoneCcw = new()
    {
        FillMode = FillMode.Solid,
        CullMode = CullMode.None,
        FrontCounterClockwise = 1,
        DepthBias = 0,
        DepthBiasClamp = 0f,
        SlopeScaledDepthBias = 0f,
        DepthClipEnable = 1,
        MultisampleEnable = 0,
        AntialiasedLineEnable = 0,
        ForcedSampleCount = 0,
        ConservativeRaster = ConservativeRasterizationMode.Off,
    };

    /// <summary>Wireframe fill, no culling, CCW front. Debug visualisations.</summary>
    public static readonly RasterizerDesc WireframeCullNoneCcw = new()
    {
        FillMode = FillMode.Wireframe,
        CullMode = CullMode.None,
        FrontCounterClockwise = 1,
        DepthBias = 0,
        DepthBiasClamp = 0f,
        SlopeScaledDepthBias = 0f,
        DepthClipEnable = 1,
        MultisampleEnable = 0,
        AntialiasedLineEnable = 0,
        ForcedSampleCount = 0,
        ConservativeRaster = ConservativeRasterizationMode.Off,
    };

    /// <summary>Shadow-pass raster: <see cref="SolidCullBackCcw"/> + adjustable depth-bias to combat acne.</summary>
    public static RasterizerDesc ShadowBiased(int depthBias, float slopeScaledDepthBias) =>
        new()
        {
            FillMode = FillMode.Solid,
            CullMode = CullMode.Back,
            FrontCounterClockwise = 1,
            DepthBias = depthBias,
            DepthBiasClamp = 0f,
            SlopeScaledDepthBias = slopeScaledDepthBias,
            DepthClipEnable = 1,
            MultisampleEnable = 0,
            AntialiasedLineEnable = 0,
            ForcedSampleCount = 0,
            ConservativeRaster = ConservativeRasterizationMode.Off,
        };
}
