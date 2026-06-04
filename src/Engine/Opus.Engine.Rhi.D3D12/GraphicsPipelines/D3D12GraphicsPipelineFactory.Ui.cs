using System;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;
using static Opus.Engine.Rhi.Direct3D12.InputElementHelpers;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>2D UI sprite PSO — the batched-quad pipeline behind <c>IDrawSurface</c> on
/// D3D12. Alpha-blended, depth-disabled, cull-none: UI draws back-to-front in submission
/// order with no z-buffer.</summary>
public static unsafe partial class D3D12GraphicsPipelineFactory
{
    /// <summary>Quad vertex stride in bytes for <see cref="CreateUiSprite"/>: position(8)
    /// + uv(8) + colour(4) + mode(4) + shape params(8). Callers pass this to
    /// <c>IASetVertexBuffer</c>.</summary>
    public const uint UiSpriteVertexStride = 32u;

    /// <summary>UI sprite quad: POSITION0(float2, pixel space) + TEXCOORD0(float2, atlas
    /// or local UV) + COLOR0(rgba8) + TEXCOORD1(float, draw mode) + TEXCOORD2(float2,
    /// shape params). 32-byte stride, alpha-blend, no depth, cull none.</summary>
    public static D3D12GraphicsPipeline CreateUiSprite(
        D3D12RhiDevice device, D3D12RootSignature rootSignature,
        ReadOnlySpan<byte> vertexShaderDxil, ReadOnlySpan<byte> pixelShaderDxil,
        Format renderTargetFormat)
    {
        fixed (byte* pPos = InputSemanticNames.Position)
        fixed (byte* pUv = InputSemanticNames.Texcoord)
        fixed (byte* pCol = InputSemanticNames.Color)
        {
            var elements = stackalloc InputElementDesc[5];
            elements[0] = Element(pPos, Format.FormatR32G32Float, byteOffset: 0u);
            elements[1] = Element(pUv, Format.FormatR32G32Float, byteOffset: 8u);
            elements[2] = Element(pCol, Format.FormatR8G8B8A8Unorm, byteOffset: 16u);
            elements[3] = Element(pUv, Format.FormatR32Float, byteOffset: 20u, semanticIndex: 1u);
            elements[4] = Element(pUv, Format.FormatR32G32Float, byteOffset: 24u, semanticIndex: 2u);

            var spec = PipelineSpec.ColourOnly(
                RasterizerPresets.SolidCullNone, BlendPresets.AlphaBlend, renderTargetFormat);
            return GraphicsPipelineBuilder.Build(
                device, rootSignature, vertexShaderDxil, pixelShaderDxil, elements, 5u, spec);
        }
    }
}
