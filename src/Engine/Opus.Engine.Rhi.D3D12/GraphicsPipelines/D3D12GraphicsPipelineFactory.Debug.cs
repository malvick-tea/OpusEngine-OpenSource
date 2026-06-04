using System;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;
using static Opus.Engine.Rhi.Direct3D12.InputElementHelpers;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>R-13.b wireframe debug PSO. POS+NORMAL+COLOR layout but Wireframe fill, cull none, CCW front.</summary>
public static unsafe partial class D3D12GraphicsPipelineFactory
{
    public static D3D12GraphicsPipeline CreatePosNormalColorWireframe(
        D3D12RhiDevice device, D3D12RootSignature rootSignature,
        ReadOnlySpan<byte> vertexShaderDxil, ReadOnlySpan<byte> pixelShaderDxil,
        Format renderTargetFormat, Format depthStencilFormat)
    {
        fixed (byte* pPos = InputSemanticNames.Position)
        fixed (byte* pNrm = InputSemanticNames.Normal)
        fixed (byte* pCol = InputSemanticNames.Color)
        {
            var elements = stackalloc InputElementDesc[3];
            elements[0] = Element(pPos, Format.FormatR32G32B32Float, byteOffset: 0u);
            elements[1] = Element(pNrm, Format.FormatR32G32B32Float, byteOffset: 12u);
            elements[2] = Element(pCol, Format.FormatR32G32B32Float, byteOffset: 24u);

            var spec = PipelineSpec.ColourAndDepth(RasterizerPresets.WireframeCullNoneCcw, BlendPresets.Opaque,
                DepthStencilPresets.LessWrite, renderTargetFormat, depthStencilFormat);
            return GraphicsPipelineBuilder.Build(device, rootSignature, vertexShaderDxil, pixelShaderDxil, elements, 3u, spec);
        }
    }
}
