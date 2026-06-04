using Silk.NET.Direct3D12;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Graphics-pipeline root-signature parameter bindings. Bind the signature
/// itself with <see cref="SetGraphicsRootSignature"/>, then push each root parameter
/// by its declared index: <see cref="SetGraphicsRootDescriptorTable"/> for SRV / UAV
/// / sampler tables, <see cref="SetGraphicsRootConstantBufferView"/> /
/// <see cref="SetGraphicsRootShaderResourceView"/> for root descriptors,
/// <see cref="SetGraphicsRoot32BitConstants{T}"/> for inlined per-draw push constants.
/// Compute pipelines have an analogous set in
/// <c>D3D12CommandList.ComputeRoot.cs</c>.</summary>
public sealed unsafe partial class D3D12CommandList
{
    public void SetGraphicsRootSignature(D3D12RootSignature rootSignature) =>
        _commandList->SetGraphicsRootSignature(rootSignature.Native);

    /// <summary>Binds an SRV table at <paramref name="rootParameterIndex"/> with the GPU
    /// handle pointing at the first descriptor in the heap range.</summary>
    public void SetGraphicsRootDescriptorTable(uint rootParameterIndex, GpuDescriptorHandle handle) =>
        _commandList->SetGraphicsRootDescriptorTable(rootParameterIndex, handle);

    /// <summary>Binds <paramref name="constantBuffer"/> as a root CBV at
    /// <paramref name="rootParameterIndex"/>. The buffer's GPU virtual address must be
    /// 256-byte aligned — automatically satisfied by upload-heap-backed
    /// <see cref="D3D12Buffer"/> instances since their starting address is page-aligned.</summary>
    public void SetGraphicsRootConstantBufferView(uint rootParameterIndex, D3D12Buffer constantBuffer) =>
        _commandList->SetGraphicsRootConstantBufferView(rootParameterIndex, constantBuffer.GpuVirtualAddress);

    /// <summary>Binds <paramref name="buffer"/> as a root SRV — direct GPU virtual address,
    /// no descriptor heap involved. The shader interprets the bound buffer based on its
    /// declaration (<c>StructuredBuffer&lt;T&gt;</c>, <c>ByteAddressBuffer</c>, etc).</summary>
    public void SetGraphicsRootShaderResourceView(uint rootParameterIndex, D3D12Buffer buffer) =>
        _commandList->SetGraphicsRootShaderResourceView(rootParameterIndex, buffer.GpuVirtualAddress);

    /// <summary>Pushes <paramref name="numValues"/> DWORDs from <paramref name="source"/> into
    /// the root signature's 32-bit-constant slot at <paramref name="rootParameterIndex"/>.
    /// Cheap per-draw push for small per-object data (world matrix = 16 DWORDs).</summary>
    public void SetGraphicsRoot32BitConstants<T>(uint rootParameterIndex, uint numValues, in T source, uint destOffsetIn32BitValues = 0u)
        where T : unmanaged
    {
        fixed (T* p = &source)
        {
            _commandList->SetGraphicsRoot32BitConstants(rootParameterIndex, numValues, p, destOffsetIn32BitValues);
        }
    }
}
