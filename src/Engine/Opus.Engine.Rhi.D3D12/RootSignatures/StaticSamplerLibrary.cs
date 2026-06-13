using Silk.NET.Direct3D12;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>
/// Reusable static-sampler descriptors. Most production root sigs need exactly
/// one of {linear-clamp, linear-wrap, comparison-less-equal}; collecting them
/// here removes 14-line StaticSamplerDesc copy-pastes from every Create* method.
/// </summary>
internal static class StaticSamplerLibrary
{
    public static StaticSamplerDesc LinearClamp(ShaderVisibility visibility, uint shaderRegister = 0u, uint registerSpace = 0u, float maxLod = 0f) =>
        new()
        {
            Filter = Filter.MinMagMipLinear,
            AddressU = TextureAddressMode.Clamp,
            AddressV = TextureAddressMode.Clamp,
            AddressW = TextureAddressMode.Clamp,
            MipLODBias = 0f,
            MaxAnisotropy = 0,
            ComparisonFunc = ComparisonFunc.Never,
            BorderColor = StaticBorderColor.OpaqueBlack,
            MinLOD = 0f,
            MaxLOD = maxLod,
            ShaderRegister = shaderRegister,
            RegisterSpace = registerSpace,
            ShaderVisibility = visibility,
        };

    public static StaticSamplerDesc LinearWrap(ShaderVisibility visibility, uint shaderRegister = 0u, uint registerSpace = 0u, float maxLod = 0f) =>
        new()
        {
            Filter = Filter.MinMagMipLinear,
            AddressU = TextureAddressMode.Wrap,
            AddressV = TextureAddressMode.Wrap,
            AddressW = TextureAddressMode.Clamp,
            MipLODBias = 0f,
            MaxAnisotropy = 0,
            ComparisonFunc = ComparisonFunc.Never,
            BorderColor = StaticBorderColor.OpaqueBlack,
            MinLOD = 0f,
            MaxLOD = maxLod,
            ShaderRegister = shaderRegister,
            RegisterSpace = registerSpace,
            ShaderVisibility = visibility,
        };

    public static StaticSamplerDesc LinearWrapAllAxes(ShaderVisibility visibility, uint shaderRegister = 0u, uint registerSpace = 0u, float maxLod = float.MaxValue) =>
        new()
        {
            Filter = Filter.MinMagMipLinear,
            AddressU = TextureAddressMode.Wrap,
            AddressV = TextureAddressMode.Wrap,
            AddressW = TextureAddressMode.Wrap,
            MipLODBias = 0f,
            MaxAnisotropy = 0,
            ComparisonFunc = ComparisonFunc.Never,
            BorderColor = StaticBorderColor.OpaqueBlack,
            MinLOD = 0f,
            MaxLOD = maxLod,
            ShaderRegister = shaderRegister,
            RegisterSpace = registerSpace,
            ShaderVisibility = visibility,
        };

    /// <summary>Anisotropic wrap sampler — the production choice for textured 3D meshes.
    /// Anisotropy keeps surfaces viewed at a grazing angle (tank side armour seen from an
    /// orbit camera) sharp where a plain trilinear sampler over-blurs them; <c>MaxLOD</c>
    /// is uncapped so the full uploaded mip chain is sampled.</summary>
    public static StaticSamplerDesc AnisotropicWrap(
        ShaderVisibility visibility,
        uint maxAnisotropy = 16u,
        uint shaderRegister = 0u,
        uint registerSpace = 0u) =>
        new()
        {
            Filter = Filter.Anisotropic,
            AddressU = TextureAddressMode.Wrap,
            AddressV = TextureAddressMode.Wrap,
            AddressW = TextureAddressMode.Wrap,
            MipLODBias = 0f,
            MaxAnisotropy = maxAnisotropy,
            ComparisonFunc = ComparisonFunc.Never,
            BorderColor = StaticBorderColor.OpaqueBlack,
            MinLOD = 0f,
            MaxLOD = float.MaxValue,
            ShaderRegister = shaderRegister,
            RegisterSpace = registerSpace,
            ShaderVisibility = visibility,
        };

    /// <summary>
    /// Comparison sampler for hardware-PCF shadow sampling. Border-addressed with
    /// <see cref="StaticBorderColor.OpaqueWhite"/> so out-of-bounds samples count
    /// as "fully lit" (no shadow).
    /// </summary>
    public static StaticSamplerDesc ComparisonLessEqual(ShaderVisibility visibility, uint shaderRegister = 0u, uint registerSpace = 0u) =>
        new()
        {
            Filter = Filter.ComparisonMinMagLinearMipPoint,
            AddressU = TextureAddressMode.Border,
            AddressV = TextureAddressMode.Border,
            AddressW = TextureAddressMode.Border,
            MipLODBias = 0f,
            MaxAnisotropy = 0,
            ComparisonFunc = ComparisonFunc.LessEqual,
            BorderColor = StaticBorderColor.OpaqueWhite,
            MinLOD = 0f,
            MaxLOD = 0f,
            ShaderRegister = shaderRegister,
            RegisterSpace = registerSpace,
            ShaderVisibility = visibility,
        };
}
