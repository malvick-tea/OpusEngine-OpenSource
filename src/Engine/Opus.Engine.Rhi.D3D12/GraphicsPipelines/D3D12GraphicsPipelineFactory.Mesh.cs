using System;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;
using static Opus.Engine.Rhi.Direct3D12.InputElementHelpers;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Lit-mesh PSOs. All share back-face cull + CCW front + depth Less write-enabled,
/// differ only in input layout.</summary>
public static unsafe partial class D3D12GraphicsPipelineFactory
{
    /// <summary>R-3.a POS+COLOR mesh, no depth. 24-byte stride.</summary>
    public static D3D12GraphicsPipeline CreatePosColor(
        D3D12RhiDevice device, D3D12RootSignature rootSignature,
        ReadOnlySpan<byte> vertexShaderDxil, ReadOnlySpan<byte> pixelShaderDxil,
        Format renderTargetFormat)
    {
        fixed (byte* pPos = InputSemanticNames.Position)
        fixed (byte* pCol = InputSemanticNames.Color)
        {
            var elements = stackalloc InputElementDesc[2];
            elements[0] = Element(pPos, Format.FormatR32G32B32Float, byteOffset: 0u);
            elements[1] = Element(pCol, Format.FormatR32G32B32Float, byteOffset: 12u);

            var spec = PipelineSpec.ColourOnly(RasterizerPresets.SolidCullNone, BlendPresets.Opaque, renderTargetFormat);
            return GraphicsPipelineBuilder.Build(device, rootSignature, vertexShaderDxil, pixelShaderDxil, elements, 2u, spec);
        }
    }

    /// <summary>R-3.b POS+COLOR depth-tested mesh, 24-byte stride.</summary>
    public static D3D12GraphicsPipeline CreatePosColorDepth(
        D3D12RhiDevice device, D3D12RootSignature rootSignature,
        ReadOnlySpan<byte> vertexShaderDxil, ReadOnlySpan<byte> pixelShaderDxil,
        Format renderTargetFormat, Format depthStencilFormat)
    {
        fixed (byte* pPos = InputSemanticNames.Position)
        fixed (byte* pCol = InputSemanticNames.Color)
        {
            var elements = stackalloc InputElementDesc[2];
            elements[0] = Element(pPos, Format.FormatR32G32B32Float, byteOffset: 0u);
            elements[1] = Element(pCol, Format.FormatR32G32B32Float, byteOffset: 12u);

            var spec = PipelineSpec.ColourAndDepth(RasterizerPresets.SolidCullBackCcw, BlendPresets.Opaque,
                DepthStencilPresets.LessWrite, renderTargetFormat, depthStencilFormat);
            return GraphicsPipelineBuilder.Build(device, rootSignature, vertexShaderDxil, pixelShaderDxil, elements, 2u, spec);
        }
    }

    /// <summary>R-1.4.b POS+UV textured triangle, no depth, cull none, 20-byte stride.</summary>
    public static D3D12GraphicsPipeline CreatePosUv(
        D3D12RhiDevice device, D3D12RootSignature rootSignature,
        ReadOnlySpan<byte> vertexShaderDxil, ReadOnlySpan<byte> pixelShaderDxil,
        Format renderTargetFormat)
    {
        fixed (byte* pPos = InputSemanticNames.Position)
        fixed (byte* pUv = InputSemanticNames.Texcoord)
        {
            var elements = stackalloc InputElementDesc[2];
            elements[0] = Element(pPos, Format.FormatR32G32B32Float, byteOffset: 0u);
            elements[1] = Element(pUv, Format.FormatR32G32Float, byteOffset: 12u);

            var spec = PipelineSpec.ColourOnly(RasterizerPresets.SolidCullNone, BlendPresets.Opaque, renderTargetFormat);
            return GraphicsPipelineBuilder.Build(device, rootSignature, vertexShaderDxil, pixelShaderDxil, elements, 2u, spec);
        }
    }

    /// <summary>R-13.c POS+UV terrain mesh, depth-tested, back-face cull, 20-byte stride.</summary>
    public static D3D12GraphicsPipeline CreatePosUvLitDepth(
        D3D12RhiDevice device, D3D12RootSignature rootSignature,
        ReadOnlySpan<byte> vertexShaderDxil, ReadOnlySpan<byte> pixelShaderDxil,
        Format renderTargetFormat, Format depthStencilFormat)
    {
        fixed (byte* pPos = InputSemanticNames.Position)
        fixed (byte* pUv = InputSemanticNames.Texcoord)
        {
            var elements = stackalloc InputElementDesc[2];
            elements[0] = Element(pPos, Format.FormatR32G32B32Float, byteOffset: 0u);
            elements[1] = Element(pUv, Format.FormatR32G32Float, byteOffset: 12u);

            var spec = PipelineSpec.ColourAndDepth(RasterizerPresets.SolidCullBackCcw, BlendPresets.Opaque,
                DepthStencilPresets.LessWrite, renderTargetFormat, depthStencilFormat);
            return GraphicsPipelineBuilder.Build(device, rootSignature, vertexShaderDxil, pixelShaderDxil, elements, 2u, spec);
        }
    }

    /// <summary>R-4 POS+NORMAL+COLOR Lambert+Phong cube, 36-byte stride.</summary>
    public static D3D12GraphicsPipeline CreatePosNormalColorLitDepth(
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

            var spec = PipelineSpec.ColourAndDepth(RasterizerPresets.SolidCullBackCcw, BlendPresets.Opaque,
                DepthStencilPresets.LessWrite, renderTargetFormat, depthStencilFormat);
            return GraphicsPipelineBuilder.Build(device, rootSignature, vertexShaderDxil, pixelShaderDxil, elements, 3u, spec);
        }
    }

    /// <summary>R-19.a POS+NORMAL+UV lit-depth mesh, 32-byte stride. Baseline for glTF meshes
    /// without tangents — Lambert + ambient + per-object tint via root constants. Uses
    /// <see cref="RasterizerPresets.SolidCullNoneCcw"/> (no back-face cull) because every
    /// glTF tank asset we ship is authored <c>doubleSided: true</c>; the cull-back default
    /// produced visible hull / hatch / side-panel dropouts on the Pz.IV. See the preset's
    /// docstring for the full rationale.</summary>
    public static D3D12GraphicsPipeline CreatePosNormalUvLitDepth(
        D3D12RhiDevice device, D3D12RootSignature rootSignature,
        ReadOnlySpan<byte> vertexShaderDxil, ReadOnlySpan<byte> pixelShaderDxil,
        Format renderTargetFormat, Format depthStencilFormat)
    {
        fixed (byte* pPos = InputSemanticNames.Position)
        fixed (byte* pNrm = InputSemanticNames.Normal)
        fixed (byte* pUv = InputSemanticNames.Texcoord)
        {
            var elements = stackalloc InputElementDesc[3];
            elements[0] = Element(pPos, Format.FormatR32G32B32Float, byteOffset: 0u);
            elements[1] = Element(pNrm, Format.FormatR32G32B32Float, byteOffset: 12u);
            elements[2] = Element(pUv, Format.FormatR32G32Float, byteOffset: 24u);

            var spec = PipelineSpec.ColourAndDepth(RasterizerPresets.SolidCullNoneCcw, BlendPresets.Opaque,
                DepthStencilPresets.LessWrite, renderTargetFormat, depthStencilFormat);
            return GraphicsPipelineBuilder.Build(device, rootSignature, vertexShaderDxil, pixelShaderDxil, elements, 3u, spec);
        }
    }

    /// <summary>R-10.a POS+NORMAL+TANGENT+UV normal-mapped PBR mesh, 44-byte stride.</summary>
    public static D3D12GraphicsPipeline CreatePosNormalTangentUvLitDepth(
        D3D12RhiDevice device, D3D12RootSignature rootSignature,
        ReadOnlySpan<byte> vertexShaderDxil, ReadOnlySpan<byte> pixelShaderDxil,
        Format renderTargetFormat, Format depthStencilFormat)
    {
        fixed (byte* pPos = InputSemanticNames.Position)
        fixed (byte* pNrm = InputSemanticNames.Normal)
        fixed (byte* pTan = InputSemanticNames.Tangent)
        fixed (byte* pUv = InputSemanticNames.Texcoord)
        {
            var elements = stackalloc InputElementDesc[4];
            elements[0] = Element(pPos, Format.FormatR32G32B32Float, byteOffset: 0u);
            elements[1] = Element(pNrm, Format.FormatR32G32B32Float, byteOffset: 12u);
            elements[2] = Element(pTan, Format.FormatR32G32B32Float, byteOffset: 24u);
            elements[3] = Element(pUv, Format.FormatR32G32Float, byteOffset: 36u);

            var spec = PipelineSpec.ColourAndDepth(RasterizerPresets.SolidCullBackCcw, BlendPresets.Opaque,
                DepthStencilPresets.LessWrite, renderTargetFormat, depthStencilFormat);
            return GraphicsPipelineBuilder.Build(device, rootSignature, vertexShaderDxil, pixelShaderDxil, elements, 4u, spec);
        }
    }
}
