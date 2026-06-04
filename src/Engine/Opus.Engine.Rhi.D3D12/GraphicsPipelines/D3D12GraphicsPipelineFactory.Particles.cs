using System;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>R-12.c billboard particle PSO. Bufferless (VS picks quad corners from SV_VertexID + per-instance from SV_InstanceID), additive blend, depth read-only.</summary>
public static unsafe partial class D3D12GraphicsPipelineFactory
{
    public static D3D12GraphicsPipeline CreateParticleBillboard(
        D3D12RhiDevice device, D3D12RootSignature rootSignature,
        ReadOnlySpan<byte> vertexShaderDxil, ReadOnlySpan<byte> pixelShaderDxil,
        Format renderTargetFormat, Format depthStencilFormat)
    {
        var spec = PipelineSpec.ColourAndDepth(RasterizerPresets.SolidCullNone, BlendPresets.Additive,
            DepthStencilPresets.LessReadOnly, renderTargetFormat, depthStencilFormat);
        return GraphicsPipelineBuilder.Build(device, rootSignature, vertexShaderDxil, pixelShaderDxil,
            inputElements: null, inputElementCount: 0u, spec);
    }
}
