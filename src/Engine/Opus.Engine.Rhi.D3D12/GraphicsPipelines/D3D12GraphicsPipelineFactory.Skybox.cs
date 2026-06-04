using System;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>R-9.d skybox PSO. Bufferless triangle at clip z = 1, depth-test <c>LessEqual</c> with
/// depth-write disabled so it only fills background pixels.</summary>
public static unsafe partial class D3D12GraphicsPipelineFactory
{
    public static D3D12GraphicsPipeline CreateSkybox(
        D3D12RhiDevice device, D3D12RootSignature rootSignature,
        ReadOnlySpan<byte> vertexShaderDxil, ReadOnlySpan<byte> pixelShaderDxil,
        Format renderTargetFormat, Format depthStencilFormat)
    {
        var spec = PipelineSpec.ColourAndDepth(RasterizerPresets.SolidCullNone, BlendPresets.Opaque,
            DepthStencilPresets.LessEqualReadOnly, renderTargetFormat, depthStencilFormat);
        return GraphicsPipelineBuilder.Build(device, rootSignature, vertexShaderDxil, pixelShaderDxil,
            inputElements: null, inputElementCount: 0u, spec);
    }
}
