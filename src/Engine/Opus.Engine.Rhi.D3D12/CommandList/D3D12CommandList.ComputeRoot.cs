using Silk.NET.Direct3D12;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Compute-pipeline root-signature parameter bindings. Symmetric with
/// <c>D3D12CommandList.GraphicsRoot.cs</c> but D3D12 keeps the binding tables
/// separate per pipeline type — graphics roots are not visible to compute shaders
/// and vice versa.</summary>
public sealed unsafe partial class D3D12CommandList
{
    public void SetComputeRootSignature(D3D12RootSignature rootSignature) =>
        _commandList->SetComputeRootSignature(rootSignature.Native);

    public void SetComputeRootDescriptorTable(uint rootParameterIndex, GpuDescriptorHandle handle) =>
        _commandList->SetComputeRootDescriptorTable(rootParameterIndex, handle);

    public void SetComputeRootConstantBufferView(uint rootParameterIndex, D3D12Buffer constantBuffer) =>
        _commandList->SetComputeRootConstantBufferView(rootParameterIndex, constantBuffer.GpuVirtualAddress);

    public void SetComputeRootShaderResourceView(uint rootParameterIndex, D3D12Buffer buffer) =>
        _commandList->SetComputeRootShaderResourceView(rootParameterIndex, buffer.GpuVirtualAddress);

    public void SetComputeRootUnorderedAccessView(uint rootParameterIndex, D3D12Buffer buffer) =>
        _commandList->SetComputeRootUnorderedAccessView(rootParameterIndex, buffer.GpuVirtualAddress);
}
