using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Input-assembly stage bindings: vertex / index buffers + primitive topology.
/// Every indexed draw needs both buffers + a topology before <see cref="D3D12CommandList.DrawIndexedInstanced"/>
/// is legal.</summary>
public sealed unsafe partial class D3D12CommandList
{
    public void IASetTriangleListTopology() =>
        _commandList->IASetPrimitiveTopology(D3DPrimitiveTopology.D3D11PrimitiveTopologyTrianglelist);

    /// <summary>Binds a single vertex buffer at slot 0 with the given <paramref name="strideBytes"/>
    /// (per-vertex byte size — sum of input layout element sizes). Multi-stream binding lands
    /// in R-3 when materials need split position / morph / skin streams.</summary>
    public void IASetVertexBuffer(D3D12Buffer vertexBuffer, uint strideBytes)
    {
        var vbv = new VertexBufferView
        {
            BufferLocation = vertexBuffer.GpuVirtualAddress,
            SizeInBytes = (uint)vertexBuffer.SizeBytes,
            StrideInBytes = strideBytes,
        };
        _commandList->IASetVertexBuffers(0u, 1u, &vbv);
    }

    /// <summary>Binds an index buffer at slot 0 with the given <paramref name="format"/>
    /// (typically <c>FormatR16Uint</c> for &lt;= 65k indices or <c>FormatR32Uint</c> otherwise).</summary>
    public void IASetIndexBuffer(D3D12Buffer indexBuffer, Format format)
    {
        var ibv = new IndexBufferView
        {
            BufferLocation = indexBuffer.GpuVirtualAddress,
            SizeInBytes = (uint)indexBuffer.SizeBytes,
            Format = format,
        };
        _commandList->IASetIndexBuffer(&ibv);
    }
}
