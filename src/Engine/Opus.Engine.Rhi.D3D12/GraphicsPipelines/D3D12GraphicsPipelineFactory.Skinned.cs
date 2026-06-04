using System;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;
using static Opus.Engine.Rhi.Direct3D12.InputElementHelpers;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>R-10.b skinned-mesh PSO. POS+NORMAL+BLENDINDICES (R8G8B8A8_UINT 4 bone indices packed)
/// + BLENDWEIGHT (R32G32B32A32_FLOAT), 44-byte stride.</summary>
public static unsafe partial class D3D12GraphicsPipelineFactory
{
    public static D3D12GraphicsPipeline CreatePosNormalBoneSkinnedLitDepth(
        D3D12RhiDevice device, D3D12RootSignature rootSignature,
        ReadOnlySpan<byte> vertexShaderDxil, ReadOnlySpan<byte> pixelShaderDxil,
        Format renderTargetFormat, Format depthStencilFormat)
    {
        fixed (byte* pPos = InputSemanticNames.Position)
        fixed (byte* pNrm = InputSemanticNames.Normal)
        fixed (byte* pIdx = InputSemanticNames.BlendIndices)
        fixed (byte* pWt = InputSemanticNames.BlendWeight)
        {
            var elements = stackalloc InputElementDesc[4];
            elements[0] = Element(pPos, Format.FormatR32G32B32Float, byteOffset: 0u);
            elements[1] = Element(pNrm, Format.FormatR32G32B32Float, byteOffset: 12u);
            elements[2] = Element(pIdx, Format.FormatR8G8B8A8Uint, byteOffset: 24u);
            elements[3] = Element(pWt, Format.FormatR32G32B32A32Float, byteOffset: 28u);

            var spec = PipelineSpec.ColourAndDepth(RasterizerPresets.SolidCullBackCcw, BlendPresets.Opaque,
                DepthStencilPresets.LessWrite, renderTargetFormat, depthStencilFormat);
            return GraphicsPipelineBuilder.Build(device, rootSignature, vertexShaderDxil, pixelShaderDxil, elements, 4u, spec);
        }
    }
}
