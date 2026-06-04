using Silk.NET.Direct3D12;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Work-emission commands: draws, compute dispatches, GPU-driven indirect
/// invocations, and resource-copy operations. Every command in this file ultimately
/// produces GPU work; nothing here is pure state setup.</summary>
public sealed unsafe partial class D3D12CommandList
{
    /// <summary>Issues a non-indexed draw of <paramref name="vertexCount"/> vertices
    /// in <paramref name="instanceCount"/> instances. Bufferless draws (vertex shader
    /// uses SV_VertexID) pass <paramref name="vertexCount"/> directly without an IA layout.</summary>
    public void DrawInstanced(uint vertexCount, uint instanceCount, uint startVertex = 0u, uint startInstance = 0u) =>
        _commandList->DrawInstanced(vertexCount, instanceCount, startVertex, startInstance);

    /// <summary>Indexed draw equivalent of <see cref="DrawInstanced"/>. Reads
    /// <paramref name="indexCount"/> indices from the bound IB starting at
    /// <paramref name="startIndex"/>, adds <paramref name="baseVertex"/> to each index
    /// before fetching from the vertex buffer.</summary>
    public void DrawIndexedInstanced(uint indexCount, uint instanceCount, uint startIndex = 0u, int baseVertex = 0, uint startInstance = 0u) =>
        _commandList->DrawIndexedInstanced(indexCount, instanceCount, startIndex, baseVertex, startInstance);

    /// <summary>Issues a compute dispatch. <paramref name="threadGroupCountX"/> /
    /// <paramref name="threadGroupCountY"/> / <paramref name="threadGroupCountZ"/> are
    /// group counts — total thread count is the product with the shader's <c>[numthreads]</c>
    /// attribute (e.g. <c>numthreads(8,8,1)</c> + dispatch <c>(32,32,1)</c> = 256×256 threads).</summary>
    public void Dispatch(uint threadGroupCountX, uint threadGroupCountY, uint threadGroupCountZ = 1u) =>
        _commandList->Dispatch(threadGroupCountX, threadGroupCountY, threadGroupCountZ);

    /// <summary>R-17 GPU-driven dispatch. Reads <paramref name="maxCommandCount"/> command records
    /// from <paramref name="argBuffer"/> starting at <paramref name="argBufferOffset"/>, interprets
    /// each record per <paramref name="signature"/>, and issues the encoded GPU command. Pass
    /// <c>null</c> for <paramref name="countBuffer"/> to always emit <c>maxCommandCount</c> commands,
    /// or supply a UAV-resident buffer whose first <c>uint</c> at <paramref name="countBufferOffset"/>
    /// caps the actual command count (compute-side culling pattern).</summary>
    public void ExecuteIndirect(
        D3D12CommandSignature signature,
        uint maxCommandCount,
        D3D12Buffer argBuffer,
        ulong argBufferOffset = 0u,
        D3D12Buffer? countBuffer = null,
        ulong countBufferOffset = 0u)
    {
        var countResource = countBuffer is null ? (ID3D12Resource*)null : countBuffer.Native;
        _commandList->ExecuteIndirect(
            signature.Native,
            maxCommandCount,
            argBuffer.Native,
            argBufferOffset,
            countResource,
            countBufferOffset);
    }

    /// <summary>Schedules a buffer→texture copy via <c>CopyTextureRegion</c>. The
    /// <paramref name="sourceLayout"/> describes the row layout in the staging buffer —
    /// typically obtained from <c>ID3D12Device::GetCopyableFootprints</c>.</summary>
    public void CopyBufferToTexture(
        D3D12Buffer source,
        PlacedSubresourceFootprint sourceLayout,
        D3D12Texture dest,
        uint destSubresource = 0u)
    {
        var src = new TextureCopyLocation { PResource = source.Native, Type = TextureCopyType.PlacedFootprint };
        src.Anonymous.PlacedFootprint = sourceLayout;

        var dst = new TextureCopyLocation { PResource = dest.Native, Type = TextureCopyType.SubresourceIndex };
        dst.Anonymous.SubresourceIndex = destSubresource;

        _commandList->CopyTextureRegion(&dst, 0u, 0u, 0u, &src, pSrcBox: null);
    }

    /// <summary>Schedules a texture→buffer copy via <c>CopyTextureRegion</c>. The
    /// destination layout is usually obtained from <c>ID3D12Device::GetCopyableFootprints</c>
    /// and the destination buffer must be a READBACK heap resource when the CPU will map it.</summary>
    public void CopyTextureToBuffer(
        ID3D12Resource* source,
        D3D12Buffer destination,
        PlacedSubresourceFootprint destinationLayout,
        uint sourceSubresource = 0u)
    {
        var dst = new TextureCopyLocation { PResource = destination.Native, Type = TextureCopyType.PlacedFootprint };
        dst.Anonymous.PlacedFootprint = destinationLayout;

        var src = new TextureCopyLocation { PResource = source, Type = TextureCopyType.SubresourceIndex };
        src.Anonymous.SubresourceIndex = sourceSubresource;

        _commandList->CopyTextureRegion(&dst, 0u, 0u, 0u, &src, pSrcBox: null);
    }

    public void CopyTextureToBuffer(
        D3D12Texture source,
        D3D12Buffer destination,
        PlacedSubresourceFootprint destinationLayout,
        uint sourceSubresource = 0u) =>
        CopyTextureToBuffer(source.Native, destination, destinationLayout, sourceSubresource);
}
