using Silk.NET.Direct3D12;
using D3D12Feature = Silk.NET.Direct3D12.Feature;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Optional-feature detection. Reads the device's CheckFeatureSupport options
/// for wave intrinsics, raytracing, VRS, mesh shaders, dynamic resources (SM 6.6).</summary>
internal static unsafe class D3D12CapabilityProbe
{
    public static RhiCapabilities Query(ID3D12Device* device)
    {
        var caps = RhiCapabilities.AsyncCompute | RhiCapabilities.AsyncCopy;

        FeatureDataD3D12Options1 options1 = default;
        if (device->CheckFeatureSupport(D3D12Feature.D3D12Options1, &options1, (uint)sizeof(FeatureDataD3D12Options1)) >= 0
            && options1.WaveOps)
        {
            caps |= RhiCapabilities.WaveIntrinsics;
        }

        FeatureDataD3D12Options5 options5 = default;
        if (device->CheckFeatureSupport(D3D12Feature.D3D12Options5, &options5, (uint)sizeof(FeatureDataD3D12Options5)) >= 0
            && (uint)options5.RaytracingTier >= (uint)RaytracingTier.Tier10)
        {
            caps |= RhiCapabilities.RayTracing;
        }

        FeatureDataD3D12Options6 options6 = default;
        if (device->CheckFeatureSupport(D3D12Feature.D3D12Options6, &options6, (uint)sizeof(FeatureDataD3D12Options6)) >= 0
            && (uint)options6.VariableShadingRateTier >= (uint)VariableShadingRateTier.Tier1)
        {
            caps |= RhiCapabilities.VariableRateShading;
        }

        FeatureDataD3D12Options7 options7 = default;
        if (device->CheckFeatureSupport(D3D12Feature.D3D12Options7, &options7, (uint)sizeof(FeatureDataD3D12Options7)) >= 0
            && (uint)options7.MeshShaderTier >= (uint)MeshShaderTier.Tier1)
        {
            caps |= RhiCapabilities.MeshShaders;
        }

        FeatureDataShaderModel sm = new() { HighestShaderModel = D3DShaderModel.D3DShaderModel66 };
        if (device->CheckFeatureSupport(D3D12Feature.ShaderModel, &sm, (uint)sizeof(FeatureDataShaderModel)) >= 0
            && (uint)sm.HighestShaderModel >= (uint)D3DShaderModel.D3DShaderModel66)
        {
            caps |= RhiCapabilities.DynamicResources;
        }

        return caps;
    }
}
