using System;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>
/// Factory for D3D12 compute pipeline state objects. Wraps the simpler
/// <c>D3D12_COMPUTE_PIPELINE_STATE_DESC</c> path: one shader + a root signature,
/// no blend/rasterizer/depth/input-layout — those don't apply to compute. R-6.a
/// ships this for the first compute-to-texture pass; tile-cull / particle compute
/// land on top in R-6.c.
/// </summary>
public static unsafe class D3D12ComputePipelineFactory
{
    public static D3D12GraphicsPipeline Create(
        D3D12RhiDevice device,
        D3D12RootSignature rootSignature,
        ReadOnlySpan<byte> computeShaderDxil)
    {
        if (device == null)
        {
            throw new ArgumentNullException(nameof(device));
        }

        if (rootSignature == null)
        {
            throw new ArgumentNullException(nameof(rootSignature));
        }

        fixed (byte* pCs = computeShaderDxil)
        {
            var desc = new ComputePipelineStateDesc
            {
                PRootSignature = rootSignature.Native,
                CS = new ShaderBytecode { PShaderBytecode = pCs, BytecodeLength = (nuint)computeShaderDxil.Length },
                NodeMask = 0,
                Flags = PipelineStateFlags.None,
            };

            var psoGuid = ID3D12PipelineState.Guid;
            ID3D12PipelineState* pso = null;
            SilkMarshal.ThrowHResult(device.NativeDevice->CreateComputePipelineState(
                &desc, &psoGuid, (void**)&pso));

            // D3D12GraphicsPipeline is the wrapper class name but functionally it just
            // wraps ID3D12PipelineState* — same for compute. Could be renamed in a refactor
            // when we have more PSO variants and want clearer naming.
            return new D3D12GraphicsPipeline(pso);
        }
    }
}
