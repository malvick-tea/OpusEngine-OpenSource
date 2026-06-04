using System;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Single-render-target / depth-stencil PSO configuration. Each preset comes
/// from the matching <c>*Presets</c> library; format-only fields are caller-specified.</summary>
internal readonly struct PipelineSpec
{
    public required RasterizerDesc Rasterizer { get; init; }
    public required BlendDesc Blend { get; init; }
    public required DepthStencilDesc DepthStencil { get; init; }
    public required Format RenderTargetFormat { get; init; }
    public Format DepthStencilFormat { get; init; }
    public PrimitiveTopologyType Topology { get; init; }
    public uint NumRenderTargets { get; init; }

    public static PipelineSpec ColourAndDepth(in RasterizerDesc raster, in RenderTargetBlendDesc blend,
        in DepthStencilDesc depth, Format renderTargetFormat, Format depthStencilFormat) =>
        new()
        {
            Rasterizer = raster,
            Blend = BlendPresets.ToBlendDesc(blend),
            DepthStencil = depth,
            RenderTargetFormat = renderTargetFormat,
            DepthStencilFormat = depthStencilFormat,
            Topology = PrimitiveTopologyType.Triangle,
            NumRenderTargets = 1u,
        };

    public static PipelineSpec ColourOnly(in RasterizerDesc raster, in RenderTargetBlendDesc blend,
        Format renderTargetFormat) =>
        new()
        {
            Rasterizer = raster,
            Blend = BlendPresets.ToBlendDesc(blend),
            DepthStencil = DepthStencilPresets.Disabled,
            RenderTargetFormat = renderTargetFormat,
            DepthStencilFormat = Format.FormatUnknown,
            Topology = PrimitiveTopologyType.Triangle,
            NumRenderTargets = 1u,
        };

    public static PipelineSpec DepthOnly(in RasterizerDesc raster, in DepthStencilDesc depth,
        Format depthStencilFormat) =>
        new()
        {
            Rasterizer = raster,
            Blend = new BlendDesc { AlphaToCoverageEnable = 0, IndependentBlendEnable = 0 },
            DepthStencil = depth,
            RenderTargetFormat = Format.FormatUnknown,
            DepthStencilFormat = depthStencilFormat,
            Topology = PrimitiveTopologyType.Triangle,
            NumRenderTargets = 0u,
        };
}

/// <summary>Single materialisation point for graphics PSOs — every Create* method in the
/// factory routes through here. Consolidates the GraphicsPipelineStateDesc setup +
/// CreateGraphicsPipelineState COM call in one place.</summary>
internal static unsafe class GraphicsPipelineBuilder
{
    public static D3D12GraphicsPipeline Build(
        D3D12RhiDevice device,
        D3D12RootSignature rootSignature,
        ReadOnlySpan<byte> vertexShaderDxil,
        ReadOnlySpan<byte> pixelShaderDxil,
        InputElementDesc* inputElements,
        uint inputElementCount,
        in PipelineSpec spec)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(rootSignature);

        fixed (byte* pVs = vertexShaderDxil)
        fixed (byte* pPs = pixelShaderDxil)
        {
            var desc = default(GraphicsPipelineStateDesc);
            desc.PRootSignature = rootSignature.Native;
            desc.VS = new ShaderBytecode { PShaderBytecode = pVs, BytecodeLength = (nuint)vertexShaderDxil.Length };
            if (!pixelShaderDxil.IsEmpty)
            {
                desc.PS = new ShaderBytecode { PShaderBytecode = pPs, BytecodeLength = (nuint)pixelShaderDxil.Length };
            }

            desc.RasterizerState = spec.Rasterizer;
            desc.BlendState = spec.Blend;
            desc.DepthStencilState = spec.DepthStencil;
            desc.SampleMask = uint.MaxValue;
            desc.PrimitiveTopologyType = spec.Topology;
            desc.NumRenderTargets = spec.NumRenderTargets;
            if (spec.NumRenderTargets > 0u)
            {
                desc.RTVFormats[0] = spec.RenderTargetFormat;
            }

            desc.DSVFormat = spec.DepthStencilFormat;
            desc.SampleDesc = new SampleDesc(1, 0);
            desc.InputLayout = new InputLayoutDesc { PInputElementDescs = inputElements, NumElements = inputElementCount };

            var psoGuid = ID3D12PipelineState.Guid;
            ID3D12PipelineState* pso = null;
            SilkMarshal.ThrowHResult(device.NativeDevice->CreateGraphicsPipelineState(&desc, &psoGuid, (void**)&pso));
            return new D3D12GraphicsPipeline(pso);
        }
    }
}
