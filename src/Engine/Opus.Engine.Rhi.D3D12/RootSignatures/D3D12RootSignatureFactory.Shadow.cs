using Silk.NET.Direct3D12;
using static Opus.Engine.Rhi.Direct3D12.DescriptorRangeBuilder;
using static Opus.Engine.Rhi.Direct3D12.RootParameterBuilder;
using static Opus.Engine.Rhi.Direct3D12.StaticSamplerLibrary;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Shadow + Forward+ root signatures.</summary>
public static unsafe partial class D3D12RootSignatureFactory
{
    private const uint ShadowWorldMatrixDwordCount = 16u; // float4x4

    /// <summary>Shadow-render pass: CBV(b0, light VP, vertex) + 16-DWORD constants(b1, world, vertex).</summary>
    public static D3D12RootSignature CreateShadowDepth(D3D12RhiDevice device)
    {
        var p = stackalloc RootParameter[2];
        p[0] = Cbv(shaderRegister: 0u, ShaderVisibility.Vertex);
        p[1] = RootConstants(shaderRegister: 1u, ShadowWorldMatrixDwordCount, ShaderVisibility.Vertex);
        return RootSignatureSerializer.Build(device, p, 2u, null, 0u, RootSignatureFlags.AllowInputAssemblerInputLayout);
    }

    /// <summary>Shadow-mapped main pass: CBV(b0, all) + 16-DWORD world(b1, all) + shadow SRV table(t0, pixel) + comparison sampler(s0, pixel, LessEqual).</summary>
    public static D3D12RootSignature CreateShadowedMain(D3D12RhiDevice device)
    {
        var shadowRange = stackalloc DescriptorRange[1];
        shadowRange[0] = SrvRange(count: 1u);

        var p = stackalloc RootParameter[3];
        p[0] = Cbv(shaderRegister: 0u, ShaderVisibility.All);
        p[1] = RootConstants(shaderRegister: 1u, ShadowWorldMatrixDwordCount, ShaderVisibility.All);
        p[2] = DescriptorTable(shadowRange, 1u, ShaderVisibility.Pixel);

        var sampler = ComparisonLessEqual(ShaderVisibility.Pixel);
        return RootSignatureSerializer.Build(device, p, 3u, &sampler, 1u, RootSignatureFlags.AllowInputAssemblerInputLayout);
    }

    /// <summary>Forward+ tile-cull compute: CBV(b0) + root SRV(t0, lights) + root UAV(u0, tile masks).</summary>
    public static D3D12RootSignature CreateForwardPlusCompute(D3D12RhiDevice device)
    {
        var p = stackalloc RootParameter[3];
        p[0] = Cbv(shaderRegister: 0u, ShaderVisibility.All);
        p[1] = Srv(shaderRegister: 0u, ShaderVisibility.All);
        p[2] = Uav(shaderRegister: 0u, ShaderVisibility.All);
        return RootSignatureSerializer.Build(device, p, 3u, null, 0u, RootSignatureFlags.None);
    }

    /// <summary>Forward+ main pass: CBV(b0) + root SRV(t0, lights) + root SRV(t1, tile masks).</summary>
    public static D3D12RootSignature CreateForwardPlusGraphics(D3D12RhiDevice device)
    {
        var p = stackalloc RootParameter[3];
        p[0] = Cbv(shaderRegister: 0u, ShaderVisibility.All);
        p[1] = Srv(shaderRegister: 0u, ShaderVisibility.All);
        p[2] = Srv(shaderRegister: 1u, ShaderVisibility.All);
        return RootSignatureSerializer.Build(device, p, 3u, null, 0u, RootSignatureFlags.AllowInputAssemblerInputLayout);
    }
}
