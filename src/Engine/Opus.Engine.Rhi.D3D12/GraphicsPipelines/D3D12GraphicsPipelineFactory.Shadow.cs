using System;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;
using static Opus.Engine.Rhi.Direct3D12.InputElementHelpers;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Shadow-pass depth-only PSO. POSITION-only input layout (12 bytes), no PS, no colour targets,
/// adjustable depth bias + slope-scaled bias to combat shadow acne.</summary>
public static unsafe partial class D3D12GraphicsPipelineFactory
{
    private const int DefaultShadowDepthBias = 1000;
    private const float DefaultShadowSlopeScaledBias = 1.0f;

    public static D3D12GraphicsPipeline CreateDepthOnlyPos(
        D3D12RhiDevice device, D3D12RootSignature rootSignature,
        ReadOnlySpan<byte> vertexShaderDxil, Format depthStencilFormat,
        int depthBias = DefaultShadowDepthBias,
        float slopeScaledDepthBias = DefaultShadowSlopeScaledBias)
    {
        fixed (byte* pPos = InputSemanticNames.Position)
        {
            var elements = stackalloc InputElementDesc[1];
            elements[0] = Element(pPos, Format.FormatR32G32B32Float, byteOffset: 0u);

            var raster = RasterizerPresets.ShadowBiased(depthBias, slopeScaledDepthBias);
            var spec = PipelineSpec.DepthOnly(raster, DepthStencilPresets.LessWrite, depthStencilFormat);
            return GraphicsPipelineBuilder.Build(device, rootSignature, vertexShaderDxil,
                pixelShaderDxil: default, elements, 1u, spec);
        }
    }
}
