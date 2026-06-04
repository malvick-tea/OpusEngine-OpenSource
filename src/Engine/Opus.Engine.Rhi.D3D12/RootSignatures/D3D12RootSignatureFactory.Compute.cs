using Silk.NET.Direct3D12;
using static Opus.Engine.Rhi.Direct3D12.DescriptorRangeBuilder;
using static Opus.Engine.Rhi.Direct3D12.RootParameterBuilder;
using static Opus.Engine.Rhi.Direct3D12.StaticSamplerLibrary;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Compute root signatures: particle sim, IBL bake passes, generic compute→UAV.</summary>
public static unsafe partial class D3D12RootSignatureFactory
{
    private const uint StandardComputeRootConstantCount = 4u;

    /// <summary>4-DWORD root constants(b0) + Root UAV(u0). R-13.a particle sim.</summary>
    public static D3D12RootSignature CreateComputeConstantsAndRootUav(D3D12RhiDevice device)
    {
        var p = stackalloc RootParameter[2];
        p[0] = RootConstants(shaderRegister: 0u, StandardComputeRootConstantCount, ShaderVisibility.All);
        p[1] = Uav(shaderRegister: 0u, ShaderVisibility.All);
        return RootSignatureSerializer.Build(device, p, 2u, null, 0u, RootSignatureFlags.None);
    }

    /// <summary>4-DWORD root constants(b0) + UAV table(u0). IBL cubemap / BRDF LUT bake.</summary>
    public static D3D12RootSignature CreateComputeConstantsAndUavTable(D3D12RhiDevice device)
    {
        var uavRange = stackalloc DescriptorRange[1];
        uavRange[0] = UavRange(count: 1u);

        var p = stackalloc RootParameter[2];
        p[0] = RootConstants(shaderRegister: 0u, StandardComputeRootConstantCount, ShaderVisibility.All);
        p[1] = DescriptorTable(uavRange, 1u, ShaderVisibility.All);
        return RootSignatureSerializer.Build(device, p, 2u, null, 0u, RootSignatureFlags.None);
    }

    /// <summary>4-DWORD root constants(b0) + SRV(t0, env) + UAV(u0, irradiance) + linear-clamp sampler. IBL irradiance bake.</summary>
    public static D3D12RootSignature CreateComputeIrradiance(D3D12RhiDevice device)
    {
        var srvRange = stackalloc DescriptorRange[1];
        srvRange[0] = SrvRange(count: 1u);
        var uavRange = stackalloc DescriptorRange[1];
        uavRange[0] = UavRange(count: 1u);

        var p = stackalloc RootParameter[3];
        p[0] = RootConstants(shaderRegister: 0u, StandardComputeRootConstantCount, ShaderVisibility.All);
        p[1] = DescriptorTable(srvRange, 1u, ShaderVisibility.All);
        p[2] = DescriptorTable(uavRange, 1u, ShaderVisibility.All);

        var sampler = LinearClamp(ShaderVisibility.All);
        return RootSignatureSerializer.Build(device, p, 3u, &sampler, 1u, RootSignatureFlags.None);
    }

    /// <summary>Single UAV table(u0). Pairs with the generic compute→texture pattern test.</summary>
    public static D3D12RootSignature CreateComputeUavTable(D3D12RhiDevice device)
    {
        var uavRange = stackalloc DescriptorRange[1];
        uavRange[0] = UavRange(count: 1u);

        var p = stackalloc RootParameter[1];
        p[0] = DescriptorTable(uavRange, 1u, ShaderVisibility.All);
        return RootSignatureSerializer.Build(device, p, 1u, null, 0u, RootSignatureFlags.None);
    }

    /// <summary>R-17.c GPU culling root sig: CBV(b0, frustum + instance count) + Root SRV(t0,
    /// instance data) + 2 Root UAVs(u0=compact transforms, u1=indirect args). Root descriptors
    /// for the buffers keep the cull dispatch heap-free.</summary>
    public static D3D12RootSignature CreateGpuCulling(D3D12RhiDevice device)
    {
        var p = stackalloc RootParameter[4];
        p[0] = Cbv(shaderRegister: 0u, ShaderVisibility.All);
        p[1] = Srv(shaderRegister: 0u, ShaderVisibility.All);
        p[2] = Uav(shaderRegister: 0u, ShaderVisibility.All);
        p[3] = Uav(shaderRegister: 1u, ShaderVisibility.All);
        return RootSignatureSerializer.Build(device, p, 4u, null, 0u, RootSignatureFlags.None);
    }
}
