using Silk.NET.Direct3D12;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Named <see cref="RenderTargetBlendDesc"/> presets. Caller wraps into a full
/// <see cref="BlendDesc"/> via <see cref="ToBlendDesc"/>.</summary>
internal static class BlendPresets
{
    /// <summary>Standard opaque write — no blending, source replaces destination.</summary>
    public static readonly RenderTargetBlendDesc Opaque = new()
    {
        BlendEnable = 0,
        LogicOpEnable = 0,
        SrcBlend = Blend.One,
        DestBlend = Blend.Zero,
        BlendOp = BlendOp.Add,
        SrcBlendAlpha = Blend.One,
        DestBlendAlpha = Blend.Zero,
        BlendOpAlpha = BlendOp.Add,
        LogicOp = LogicOp.Noop,
        RenderTargetWriteMask = (byte)ColorWriteEnable.All,
    };

    /// <summary>One/one additive for both RGB and alpha — particles, bloom upsample, emissive overlays.</summary>
    public static readonly RenderTargetBlendDesc Additive = new()
    {
        BlendEnable = 1,
        LogicOpEnable = 0,
        SrcBlend = Blend.One,
        DestBlend = Blend.One,
        BlendOp = BlendOp.Add,
        SrcBlendAlpha = Blend.One,
        DestBlendAlpha = Blend.One,
        BlendOpAlpha = BlendOp.Add,
        LogicOp = LogicOp.Noop,
        RenderTargetWriteMask = (byte)ColorWriteEnable.All,
    };

    /// <summary>Standard "over" alpha blend: <c>src.rgb*src.a + dst.rgb*(1-src.a)</c> — decals, UI, transparent geometry.</summary>
    public static readonly RenderTargetBlendDesc AlphaBlend = new()
    {
        BlendEnable = 1,
        LogicOpEnable = 0,
        SrcBlend = Blend.SrcAlpha,
        DestBlend = Blend.InvSrcAlpha,
        BlendOp = BlendOp.Add,
        SrcBlendAlpha = Blend.One,
        DestBlendAlpha = Blend.InvSrcAlpha,
        BlendOpAlpha = BlendOp.Add,
        LogicOp = LogicOp.Noop,
        RenderTargetWriteMask = (byte)ColorWriteEnable.All,
    };

    /// <summary>Wrap a per-RT blend in a single-RT <see cref="BlendDesc"/> (the common case).</summary>
    public static BlendDesc ToBlendDesc(RenderTargetBlendDesc rt0)
    {
        var blend = new BlendDesc { AlphaToCoverageEnable = 0, IndependentBlendEnable = 0 };
        blend.RenderTarget[0] = rt0;
        return blend;
    }
}
