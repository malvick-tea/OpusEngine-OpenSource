using Silk.NET.Direct3D12;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Named <see cref="DepthStencilDesc"/> presets.</summary>
internal static class DepthStencilPresets
{
    /// <summary>Depth + stencil both off. Used by bufferless / fullscreen post / un-depthed colour tests.</summary>
    public static readonly DepthStencilDesc Disabled = new()
    {
        DepthEnable = 0,
        StencilEnable = 0,
    };

    /// <summary>Depth Less, write enabled. Standard for opaque scene geometry.</summary>
    public static readonly DepthStencilDesc LessWrite = new()
    {
        DepthEnable = 1,
        DepthWriteMask = DepthWriteMask.All,
        DepthFunc = ComparisonFunc.Less,
        StencilEnable = 0,
    };

    /// <summary>Depth Less, write disabled. Particles / overlays — tested but don't occlude each other.</summary>
    public static readonly DepthStencilDesc LessReadOnly = new()
    {
        DepthEnable = 1,
        DepthWriteMask = DepthWriteMask.Zero,
        DepthFunc = ComparisonFunc.Less,
        StencilEnable = 0,
    };

    /// <summary>Depth LessEqual, write disabled. Skybox at clip z = 1 fills only depth == 1 (background) pixels.</summary>
    public static readonly DepthStencilDesc LessEqualReadOnly = new()
    {
        DepthEnable = 1,
        DepthWriteMask = DepthWriteMask.Zero,
        DepthFunc = ComparisonFunc.LessEqual,
        StencilEnable = 0,
    };
}
