using System;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Bufferless triangle / fullscreen post-process / fullscreen additive PSOs.
/// No input layout — VS positions vertices procedurally via <c>SV_VertexID</c>.</summary>
public static unsafe partial class D3D12GraphicsPipelineFactory
{
    /// <summary>R-1.3.a bufferless triangle. No IA, no depth, opaque blend, cull none.</summary>
    public static D3D12GraphicsPipeline CreateBufferlessTriangle(
        D3D12RhiDevice device, D3D12RootSignature rootSignature,
        ReadOnlySpan<byte> vertexShaderDxil, ReadOnlySpan<byte> pixelShaderDxil,
        Format renderTargetFormat)
    {
        var spec = PipelineSpec.ColourOnly(RasterizerPresets.SolidCullNone, BlendPresets.Opaque, renderTargetFormat);
        return GraphicsPipelineBuilder.Build(device, rootSignature, vertexShaderDxil, pixelShaderDxil,
            inputElements: null, inputElementCount: 0u, spec);
    }

    /// <summary>Fullscreen post-process pass. Opaque blend, no depth, cull none — VS draws over-screen triangle.</summary>
    public static D3D12GraphicsPipeline CreateFullscreenPostProcess(
        D3D12RhiDevice device, D3D12RootSignature rootSignature,
        ReadOnlySpan<byte> vertexShaderDxil, ReadOnlySpan<byte> pixelShaderDxil,
        Format renderTargetFormat)
    {
        var spec = PipelineSpec.ColourOnly(RasterizerPresets.SolidCullNone, BlendPresets.Opaque, renderTargetFormat);
        return GraphicsPipelineBuilder.Build(device, rootSignature, vertexShaderDxil, pixelShaderDxil,
            inputElements: null, inputElementCount: 0u, spec);
    }

    /// <summary>Additive (<c>One/One</c>) variant of <see cref="CreateFullscreenPostProcess"/> — bloom upsample.</summary>
    public static D3D12GraphicsPipeline CreateFullscreenPostProcessAdditive(
        D3D12RhiDevice device, D3D12RootSignature rootSignature,
        ReadOnlySpan<byte> vertexShaderDxil, ReadOnlySpan<byte> pixelShaderDxil,
        Format renderTargetFormat)
    {
        var spec = PipelineSpec.ColourOnly(RasterizerPresets.SolidCullNone, BlendPresets.Additive, renderTargetFormat);
        return GraphicsPipelineBuilder.Build(device, rootSignature, vertexShaderDxil, pixelShaderDxil,
            inputElements: null, inputElementCount: 0u, spec);
    }

    /// <summary>Standard "over" alpha-blend (<c>SrcAlpha/InvSrcAlpha</c>) variant — decals, transparent UI.</summary>
    public static D3D12GraphicsPipeline CreateFullscreenAlphaBlend(
        D3D12RhiDevice device, D3D12RootSignature rootSignature,
        ReadOnlySpan<byte> vertexShaderDxil, ReadOnlySpan<byte> pixelShaderDxil,
        Format renderTargetFormat)
    {
        var spec = PipelineSpec.ColourOnly(RasterizerPresets.SolidCullNone, BlendPresets.AlphaBlend, renderTargetFormat);
        return GraphicsPipelineBuilder.Build(device, rootSignature, vertexShaderDxil, pixelShaderDxil,
            inputElements: null, inputElementCount: 0u, spec);
    }
}
