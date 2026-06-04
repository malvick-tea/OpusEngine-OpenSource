using Silk.NET.Direct3D12;
using static Opus.Engine.Rhi.Direct3D12.DescriptorRangeBuilder;
using static Opus.Engine.Rhi.Direct3D12.RootParameterBuilder;
using static Opus.Engine.Rhi.Direct3D12.StaticSamplerLibrary;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Foundational root signatures used by the bootstrap triangle/cube tests
/// and the simplest material pipelines. No PBR / IBL / post / shadow knowledge here.</summary>
public static unsafe partial class D3D12RootSignatureFactory
{
    /// <summary>Empty root signature for bufferless geometry (R-1.3 sandbox).</summary>
    public static D3D12RootSignature CreateEmpty(D3D12RhiDevice device) =>
        RootSignatureSerializer.Build(device, parameters: null, parameterCount: 0u,
            staticSamplers: null, staticSamplerCount: 0u,
            flags: RootSignatureFlags.AllowInputAssemblerInputLayout);

    /// <summary>Single root CBV at b0 (vertex-only). The cheapest per-frame matrix binding.</summary>
    public static D3D12RootSignature CreateCbvVertex(D3D12RhiDevice device)
    {
        var p = stackalloc RootParameter[1];
        p[0] = Cbv(shaderRegister: 0u, ShaderVisibility.Vertex);
        return RootSignatureSerializer.Build(device, p, 1u, null, 0u, RootSignatureFlags.AllowInputAssemblerInputLayout);
    }

    /// <summary>Single root CBV at b0, all-stage visibility. Used when VS + PS share a CB.</summary>
    public static D3D12RootSignature CreateCbvAll(D3D12RhiDevice device)
    {
        var p = stackalloc RootParameter[1];
        p[0] = Cbv(shaderRegister: 0u, ShaderVisibility.All);
        return RootSignatureSerializer.Build(device, p, 1u, null, 0u, RootSignatureFlags.AllowInputAssemblerInputLayout);
    }

    /// <summary>CBV(b0) + root SRV(t0), both all-stage visible. R-6.b multi-light forward shading.</summary>
    public static D3D12RootSignature CreateCbvSrvAll(D3D12RhiDevice device)
    {
        var p = stackalloc RootParameter[2];
        p[0] = Cbv(shaderRegister: 0u, ShaderVisibility.All);
        p[1] = Srv(shaderRegister: 0u, ShaderVisibility.All);
        return RootSignatureSerializer.Build(device, p, 2u, null, 0u, RootSignatureFlags.AllowInputAssemblerInputLayout);
    }

    /// <summary>CBV(b0) + N-DWORD root constants(b1, default 16 = world matrix), all-stage.</summary>
    public static D3D12RootSignature CreateCbvWith32BitConstants(D3D12RhiDevice device, uint num32BitValues = 16u)
    {
        var p = stackalloc RootParameter[2];
        p[0] = Cbv(shaderRegister: 0u, ShaderVisibility.All);
        p[1] = RootConstants(shaderRegister: 1u, num32BitValues, ShaderVisibility.All);
        return RootSignatureSerializer.Build(device, p, 2u, null, 0u, RootSignatureFlags.AllowInputAssemblerInputLayout);
    }

    /// <summary>SRV table(t0, pixel) + static linear-wrap sampler. R-1.4.b textured triangle.</summary>
    public static D3D12RootSignature CreateSrvWithStaticSampler(D3D12RhiDevice device)
    {
        var srvRange = stackalloc DescriptorRange[1];
        srvRange[0] = SrvRange(count: 1u);

        var p = stackalloc RootParameter[1];
        p[0] = DescriptorTable(srvRange, 1u, ShaderVisibility.Pixel);

        var sampler = LinearWrapAllAxes(ShaderVisibility.Pixel);
        return RootSignatureSerializer.Build(device, p, 1u, &sampler, 1u, RootSignatureFlags.AllowInputAssemblerInputLayout);
    }

    /// <summary>R-19.b textured lit mesh: CBV(b0, all — scene) + N-DWORD root constants(b1, all
    /// — world + tint, default 20) + 1-SRV table(t0, pixel — baseColor) + static
    /// anisotropic-wrap sampler. Used by the real textured tank pass; replaces the per-mesh
    /// palette tint of R-19.a with a sampled albedo while keeping the rest of the binding
    /// layout. The anisotropic sampler pairs with the mip-chained atlas textures (Phase 34)
    /// so camo detail stays crisp instead of aliasing into a washed-out flat surface.</summary>
    public static D3D12RootSignature CreateCbvConstantsAndTexture(D3D12RhiDevice device, uint num32BitValues = 20u)
    {
        var srvRange = stackalloc DescriptorRange[1];
        srvRange[0] = SrvRange(count: 1u);

        var p = stackalloc RootParameter[3];
        p[0] = Cbv(shaderRegister: 0u, ShaderVisibility.All);
        p[1] = RootConstants(shaderRegister: 1u, num32BitValues, ShaderVisibility.All);
        p[2] = DescriptorTable(srvRange, 1u, ShaderVisibility.Pixel);

        var sampler = AnisotropicWrap(ShaderVisibility.Pixel);
        return RootSignatureSerializer.Build(device, p, 3u, &sampler, 1u, RootSignatureFlags.AllowInputAssemblerInputLayout);
    }

    /// <summary>R-19.c GPU-instanced textured lit mesh: the <see cref="CreateCbvConstantsAndTexture"/>
    /// layout plus a root SRV at <c>t1</c> (vertex-visible) for the per-frame instance
    /// <c>StructuredBuffer</c>. CBV(b0, all — scene) + N-DWORD root constants(b1, all — per-draw
    /// material factor + instance offset, default 5) + 1-SRV table(t0, pixel — baseColor) + root
    /// SRV(t1, vertex — per-instance world + tint) + static anisotropic-wrap sampler. One
    /// <c>DrawIndexedInstanced</c> per mesh primitive fans across every instance, each reading its
    /// world + tint from <c>t1</c> at <c>InstanceOffset + SV_InstanceID</c>.</summary>
    public static D3D12RootSignature CreateInstancedCbvConstantsTexture(D3D12RhiDevice device, uint num32BitValues = 5u)
    {
        var srvRange = stackalloc DescriptorRange[1];
        srvRange[0] = SrvRange(count: 1u);

        var p = stackalloc RootParameter[4];
        p[0] = Cbv(shaderRegister: 0u, ShaderVisibility.All);
        p[1] = RootConstants(shaderRegister: 1u, num32BitValues, ShaderVisibility.All);
        p[2] = DescriptorTable(srvRange, 1u, ShaderVisibility.Pixel);
        p[3] = Srv(shaderRegister: 1u, ShaderVisibility.Vertex);

        var sampler = AnisotropicWrap(ShaderVisibility.Pixel);
        return RootSignatureSerializer.Build(device, p, 4u, &sampler, 1u, RootSignatureFlags.AllowInputAssemblerInputLayout);
    }
}
