using Silk.NET.Direct3D12;
using static Opus.Engine.Rhi.Direct3D12.DescriptorRangeBuilder;
using static Opus.Engine.Rhi.Direct3D12.RootParameterBuilder;
using static Opus.Engine.Rhi.Direct3D12.StaticSamplerLibrary;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>PBR + IBL scene root signatures. All share the shape:
/// CBV(b0, scene) + root constants(b1, per-object) + SRV table(s) for IBL + optional
/// per-material table.</summary>
public static unsafe partial class D3D12RootSignatureFactory
{
    private const uint PbrWorldOnlyRootConstants = 16u; // 1 float4x4
    private const uint PbrWorldPlusMaterialRootConstants = 24u; // 1 float4x4 + albedo/metallic/roughness/ao packed
    private const uint IblTextureCount = 3u; // irradiance + prefilter + brdfLut
    private const uint IblPlusNormalMapTextureCount = 4u;
    private const uint MaterialTextureCount = 3u; // albedo + normal + MRA
    private const uint InstancedPbrMapCount = 5u; // base + normal + metallic-roughness + occlusion + emissive
    private const uint InstancedPbrInstanceRegister = InstancedPbrMapCount; // instance SRV sits just past the map table (t5)

    /// <summary>GPU-instanced forward PBR mesh: CBV(b0, all — scene) + N-DWORD root constants(b1,
    /// all — per-draw base/metal-rough/emissive factors + instance offset) + 5-SRV material table
    /// (t0..t4, pixel — base colour / normal / metallic-roughness / occlusion / emissive) + root
    /// SRV(t5, vertex — per-instance world + tint) + static anisotropic-wrap sampler. The
    /// metal-roughness analogue of <see cref="CreateInstancedCbvConstantsTexture"/>: the single
    /// albedo SRV grows to the full map run while the root-parameter indices stay put, so the pass
    /// binds the table at index 2 and the instance buffer at index 3 unchanged. One
    /// <c>DrawIndexedInstanced</c> per mesh primitive fans across every instance.</summary>
    public static D3D12RootSignature CreateInstancedPbrScene(D3D12RhiDevice device, uint num32BitValues)
    {
        var mapRange = stackalloc DescriptorRange[1];
        mapRange[0] = SrvRange(count: InstancedPbrMapCount);

        var p = stackalloc RootParameter[4];
        p[0] = Cbv(shaderRegister: 0u, ShaderVisibility.All);
        p[1] = RootConstants(shaderRegister: 1u, num32BitValues, ShaderVisibility.All);
        p[2] = DescriptorTable(mapRange, 1u, ShaderVisibility.Pixel);
        p[3] = Srv(shaderRegister: InstancedPbrInstanceRegister, ShaderVisibility.Vertex);

        var sampler = AnisotropicWrap(ShaderVisibility.Pixel);
        return RootSignatureSerializer.Build(device, p, 4u, &sampler, 1u, RootSignatureFlags.AllowInputAssemblerInputLayout);
    }

    /// <summary>CBV(b0) + 24-DWORD constants(b1, world+material) + IBL SRV table(t0..t2) + linear-clamp(s0, pixel).</summary>
    public static D3D12RootSignature CreatePbrIblScene(D3D12RhiDevice device)
    {
        var iblRange = stackalloc DescriptorRange[1];
        iblRange[0] = SrvRange(count: IblTextureCount);

        var p = stackalloc RootParameter[3];
        p[0] = Cbv(shaderRegister: 0u, ShaderVisibility.All);
        p[1] = RootConstants(shaderRegister: 1u, PbrWorldPlusMaterialRootConstants, ShaderVisibility.All);
        p[2] = DescriptorTable(iblRange, 1u, ShaderVisibility.Pixel);

        var sampler = LinearClamp(ShaderVisibility.Pixel);
        return RootSignatureSerializer.Build(device, p, 3u, &sampler, 1u, RootSignatureFlags.AllowInputAssemblerInputLayout);
    }

    /// <summary>R-10.c — CBV(b0) + 16-DWORD constants(b1, world only) + IBL table(t0..t2) + material table(t3..t5) + linear-wrap(s0, pixel).</summary>
    public static D3D12RootSignature CreatePbrIblSceneWithMaterial(D3D12RhiDevice device)
    {
        var iblRange = stackalloc DescriptorRange[1];
        iblRange[0] = SrvRange(count: IblTextureCount, baseShaderRegister: 0u);
        var matRange = stackalloc DescriptorRange[1];
        matRange[0] = SrvRange(count: MaterialTextureCount, baseShaderRegister: IblTextureCount);

        var p = stackalloc RootParameter[4];
        p[0] = Cbv(shaderRegister: 0u, ShaderVisibility.All);
        p[1] = RootConstants(shaderRegister: 1u, PbrWorldOnlyRootConstants, ShaderVisibility.All);
        p[2] = DescriptorTable(iblRange, 1u, ShaderVisibility.Pixel);
        p[3] = DescriptorTable(matRange, 1u, ShaderVisibility.Pixel);

        var sampler = LinearWrap(ShaderVisibility.Pixel);
        return RootSignatureSerializer.Build(device, p, 4u, &sampler, 1u, RootSignatureFlags.AllowInputAssemblerInputLayout);
    }

    /// <summary>R-10.a — CBV(b0) + 24-DWORD constants(b1) + IBL+normal table(t0..t3) + linear-wrap(s0, pixel).</summary>
    public static D3D12RootSignature CreatePbrIblSceneNormalMapped(D3D12RhiDevice device)
    {
        var iblRange = stackalloc DescriptorRange[1];
        iblRange[0] = SrvRange(count: IblPlusNormalMapTextureCount);

        var p = stackalloc RootParameter[3];
        p[0] = Cbv(shaderRegister: 0u, ShaderVisibility.All);
        p[1] = RootConstants(shaderRegister: 1u, PbrWorldPlusMaterialRootConstants, ShaderVisibility.All);
        p[2] = DescriptorTable(iblRange, 1u, ShaderVisibility.Pixel);

        var sampler = LinearWrap(ShaderVisibility.Pixel);
        return RootSignatureSerializer.Build(device, p, 3u, &sampler, 1u, RootSignatureFlags.AllowInputAssemblerInputLayout);
    }
}
