using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Descriptor heap factories. RTV / DSV are CPU-only (bound by handle, not visible
/// to shaders); CBV/SRV/UAV are shader-visible because they live in bindings.</summary>
public sealed unsafe partial class D3D12RhiDevice
{
    public ID3D12DescriptorHeap* CreateRtvDescriptorHeap(uint capacity) =>
        CreateHeap(DescriptorHeapType.Rtv, capacity, shaderVisible: false);

    public ID3D12DescriptorHeap* CreateDsvDescriptorHeap(uint capacity) =>
        CreateHeap(DescriptorHeapType.Dsv, capacity, shaderVisible: false);

    public ID3D12DescriptorHeap* CreateSrvDescriptorHeap(uint capacity) =>
        CreateHeap(DescriptorHeapType.CbvSrvUav, capacity, shaderVisible: true);

    private ID3D12DescriptorHeap* CreateHeap(DescriptorHeapType type, uint capacity, bool shaderVisible)
    {
        var heapDesc = new DescriptorHeapDesc
        {
            Type = type,
            NumDescriptors = capacity,
            Flags = shaderVisible ? DescriptorHeapFlags.ShaderVisible : DescriptorHeapFlags.None,
            NodeMask = 0,
        };

        ID3D12DescriptorHeap* heap = null;
        var heapGuid = ID3D12DescriptorHeap.Guid;
        SilkMarshal.ThrowHResult(_device->CreateDescriptorHeap(&heapDesc, &heapGuid, (void**)&heap));
        return heap;
    }
}
