using Silk.NET.Direct3D12;
using static Opus.Engine.Rhi.Direct3D12.DescriptorRangeBuilder;
using static Opus.Engine.Rhi.Direct3D12.RootParameterBuilder;
using static Opus.Engine.Rhi.Direct3D12.StaticSamplerLibrary;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Terrain root signature — distinguished from <see cref="CreateShadowedMain"/> only by
/// the SRV being visible to ALL stages so the VS can sample the heightmap to displace Y.</summary>
public static unsafe partial class D3D12RootSignatureFactory
{
    private const uint TerrainWorldMatrixDwordCount = 16u;

    /// <summary>Terrain heightmap: CBV(b0, all) + 16-DWORD world(b1, all) + heightmap SRV(t0, all) + linear-clamp(s0, all).</summary>
    public static D3D12RootSignature CreateTerrainScene(D3D12RhiDevice device)
    {
        var srvRange = stackalloc DescriptorRange[1];
        srvRange[0] = SrvRange(count: 1u);

        var p = stackalloc RootParameter[3];
        p[0] = Cbv(shaderRegister: 0u, ShaderVisibility.All);
        p[1] = RootConstants(shaderRegister: 1u, TerrainWorldMatrixDwordCount, ShaderVisibility.All);
        p[2] = DescriptorTable(srvRange, 1u, ShaderVisibility.All);

        var sampler = LinearClamp(ShaderVisibility.All);
        return RootSignatureSerializer.Build(device, p, 3u, &sampler, 1u, RootSignatureFlags.AllowInputAssemblerInputLayout);
    }
}
