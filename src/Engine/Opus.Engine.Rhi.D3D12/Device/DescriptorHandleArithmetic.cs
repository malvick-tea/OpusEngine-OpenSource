using Silk.NET.Direct3D12;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Descriptor heap slot indexing primitives. Used by every view-creation method
/// to compute the CPU/GPU handle for a given slot index.</summary>
internal static unsafe class DescriptorHandleArithmetic
{
    public static CpuDescriptorHandle CpuSlot(ID3D12Device* device, ID3D12DescriptorHeap* heap, DescriptorHeapType heapType, uint slotIndex)
    {
        var handle = heap->GetCPUDescriptorHandleForHeapStart();
        handle.Ptr += slotIndex * device->GetDescriptorHandleIncrementSize(heapType);
        return handle;
    }

    public static GpuDescriptorHandle GpuSlot(ID3D12Device* device, ID3D12DescriptorHeap* heap, DescriptorHeapType heapType, uint slotIndex)
    {
        var handle = heap->GetGPUDescriptorHandleForHeapStart();
        handle.Ptr += slotIndex * device->GetDescriptorHandleIncrementSize(heapType);
        return handle;
    }

    /// <summary>The default RGBA component mapping for SRVs — channels map straight through.
    /// Equivalent of <c>D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING</c> which Silk.NET doesn't expose.</summary>
    public const uint DefaultShader4ComponentMapping = (0 << 0) | (1 << 3) | (2 << 6) | (3 << 9) | (1u << 12);
}
