using Silk.NET.Direct3D12;
using Silk.NET.DXGI;
using static Opus.Engine.Rhi.Direct3D12.DescriptorHandleArithmetic;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Render-target / depth-stencil / shader-resource / unordered-access view writers.
/// All return the GPU handle of the heap slot (or CPU handle for RTV/DSV variants since those
/// aren't shader-visible).</summary>
public sealed unsafe partial class D3D12RhiDevice
{
    public CpuDescriptorHandle CreateRenderTargetView(D3D12Texture texture, ID3D12DescriptorHeap* heap, uint slotIndex = 0u)
    {
        var cpu = CpuSlot(_device, heap, DescriptorHeapType.Rtv, slotIndex);
        _device->CreateRenderTargetView(texture.Native, pDesc: null, cpu);
        return cpu;
    }

    public CpuDescriptorHandle CreateDepthStencilView(D3D12Texture depth, ID3D12DescriptorHeap* heap)
    {
        var dsvDesc = new DepthStencilViewDesc
        {
            Format = depth.DxgiFormat,
            ViewDimension = DsvDimension.Texture2D,
            Flags = DsvFlags.None,
        };
        dsvDesc.Anonymous.Texture2D = new Tex2DDsv { MipSlice = 0 };

        var cpu = heap->GetCPUDescriptorHandleForHeapStart();
        _device->CreateDepthStencilView(depth.Native, &dsvDesc, cpu);
        return cpu;
    }

    /// <summary>DSV targeting one slice of a depth array — used to render each cascade's
    /// depth into its own slice.</summary>
    public CpuDescriptorHandle CreateDepthStencilViewForSlice(D3D12Texture depthArray, ID3D12DescriptorHeap* heap, uint slotIndex, uint sliceIndex)
    {
        var dsvDesc = new DepthStencilViewDesc
        {
            Format = depthArray.DxgiFormat,
            ViewDimension = DsvDimension.Texture2Darray,
            Flags = DsvFlags.None,
        };
        dsvDesc.Anonymous.Texture2DArray = new Tex2DArrayDsv
        {
            MipSlice = 0,
            FirstArraySlice = sliceIndex,
            ArraySize = 1,
        };

        var cpu = CpuSlot(_device, heap, DescriptorHeapType.Dsv, slotIndex);
        _device->CreateDepthStencilView(depthArray.Native, &dsvDesc, cpu);
        return cpu;
    }

    public GpuDescriptorHandle CreateShaderResourceView(D3D12Texture texture, ID3D12DescriptorHeap* heap, uint slotIndex = 0u)
    {
        var srvDesc = new ShaderResourceViewDesc
        {
            Format = texture.DxgiFormat,
            ViewDimension = SrvDimension.Texture2D,
            Shader4ComponentMapping = DefaultShader4ComponentMapping,
        };
        srvDesc.Anonymous.Texture2D = new Tex2DSrv
        {
            MostDetailedMip = 0,
            MipLevels = (uint)texture.MipLevels,
            PlaneSlice = 0,
            ResourceMinLODClamp = 0f,
        };

        return WriteShaderResourceView(texture.Native, &srvDesc, heap, slotIndex);
    }

    /// <summary>Cross-format SRV for D32_FLOAT depth — resource stays depth (for DSV),
    /// SRV view as R32_FLOAT so the PS samples it as a regular float.</summary>
    public GpuDescriptorHandle CreateDepthShaderResourceView(D3D12Texture texture, ID3D12DescriptorHeap* heap, uint slotIndex = 0u)
    {
        var srvDesc = new ShaderResourceViewDesc
        {
            Format = Format.FormatR32Float,
            ViewDimension = SrvDimension.Texture2D,
            Shader4ComponentMapping = DefaultShader4ComponentMapping,
        };
        srvDesc.Anonymous.Texture2D = new Tex2DSrv
        {
            MostDetailedMip = 0,
            MipLevels = 1,
            PlaneSlice = 0,
            ResourceMinLODClamp = 0f,
        };

        return WriteShaderResourceView(texture.Native, &srvDesc, heap, slotIndex);
    }

    public GpuDescriptorHandle CreateShaderResourceViewCubemap(D3D12Texture cubemap, ID3D12DescriptorHeap* heap, uint slotIndex = 0u)
    {
        var srvDesc = new ShaderResourceViewDesc
        {
            Format = cubemap.DxgiFormat,
            ViewDimension = SrvDimension.Texturecube,
            Shader4ComponentMapping = DefaultShader4ComponentMapping,
        };
        srvDesc.Anonymous.TextureCube = new TexcubeSrv
        {
            MostDetailedMip = 0,
            MipLevels = (uint)cubemap.MipLevels,
            ResourceMinLODClamp = 0f,
        };

        return WriteShaderResourceView(cubemap.Native, &srvDesc, heap, slotIndex);
    }

    /// <summary>Texture2DArray SRV viewing depth as R32_FLOAT — pairs with cascaded-shadow
    /// PS that calls <c>SampleCmpLevelZero(sampler, uv, sliceIndex, depthRef)</c>.</summary>
    public GpuDescriptorHandle CreateDepthShaderResourceViewForArray(D3D12Texture depthArray, ID3D12DescriptorHeap* heap, uint slotIndex = 0u)
    {
        var srvDesc = new ShaderResourceViewDesc
        {
            Format = Format.FormatR32Float,
            ViewDimension = SrvDimension.Texture2Darray,
            Shader4ComponentMapping = DefaultShader4ComponentMapping,
        };
        srvDesc.Anonymous.Texture2DArray = new Tex2DArraySrv
        {
            MostDetailedMip = 0,
            MipLevels = 1,
            FirstArraySlice = 0,
            ArraySize = (uint)depthArray.ArraySize,
            PlaneSlice = 0,
            ResourceMinLODClamp = 0f,
        };

        return WriteShaderResourceView(depthArray.Native, &srvDesc, heap, slotIndex);
    }

    public GpuDescriptorHandle CreateUnorderedAccessView(D3D12Texture texture, ID3D12DescriptorHeap* heap, uint slotIndex = 0u)
    {
        var uavDesc = new UnorderedAccessViewDesc
        {
            Format = texture.DxgiFormat,
            ViewDimension = UavDimension.Texture2D,
        };
        uavDesc.Anonymous.Texture2D = new Tex2DUav { MipSlice = 0, PlaneSlice = 0 };

        return WriteUnorderedAccessView(texture.Native, &uavDesc, heap, slotIndex);
    }

    /// <summary>Single-mip-slice SRV — R-17.b Hi-Z pyramid binds one of these per Hi-Z gen
    /// dispatch (reads mip K to produce mip K+1).</summary>
    public GpuDescriptorHandle CreateShaderResourceViewMip(D3D12Texture texture, ID3D12DescriptorHeap* heap, uint slotIndex, uint mipSlice)
    {
        var srvDesc = new ShaderResourceViewDesc
        {
            Format = texture.DxgiFormat,
            ViewDimension = SrvDimension.Texture2D,
            Shader4ComponentMapping = DefaultShader4ComponentMapping,
        };
        srvDesc.Anonymous.Texture2D = new Tex2DSrv
        {
            MostDetailedMip = mipSlice,
            MipLevels = 1,
            PlaneSlice = 0,
            ResourceMinLODClamp = 0f,
        };

        return WriteShaderResourceView(texture.Native, &srvDesc, heap, slotIndex);
    }

    /// <summary>Single-mip-slice UAV — pairs with <see cref="CreateShaderResourceViewMip"/>
    /// when ping-ponging through a mip chain on a non-cubemap 2D texture.</summary>
    public GpuDescriptorHandle CreateUnorderedAccessViewMip(D3D12Texture texture, ID3D12DescriptorHeap* heap, uint slotIndex, uint mipSlice)
    {
        var uavDesc = new UnorderedAccessViewDesc
        {
            Format = texture.DxgiFormat,
            ViewDimension = UavDimension.Texture2D,
        };
        uavDesc.Anonymous.Texture2D = new Tex2DUav { MipSlice = mipSlice, PlaneSlice = 0 };

        return WriteUnorderedAccessView(texture.Native, &uavDesc, heap, slotIndex);
    }

    /// <summary>Texture2DArray UAV covering all 6 cubemap faces — compute writes per-face
    /// via <c>RWTexture2DArray&lt;float4&gt;</c>.</summary>
    public GpuDescriptorHandle CreateUnorderedAccessViewForCubeArray(D3D12Texture cubemap, ID3D12DescriptorHeap* heap, uint slotIndex = 0u)
    {
        var uavDesc = new UnorderedAccessViewDesc
        {
            Format = cubemap.DxgiFormat,
            ViewDimension = UavDimension.Texture2Darray,
        };
        uavDesc.Anonymous.Texture2DArray = new Tex2DArrayUav
        {
            MipSlice = 0,
            FirstArraySlice = 0,
            ArraySize = 6,
            PlaneSlice = 0,
        };

        return WriteUnorderedAccessView(cubemap.Native, &uavDesc, heap, slotIndex);
    }

    /// <summary>Per-mip UAV for a mip-chained cubemap — same Texture2DArray view but
    /// addressing one mip slice. R-9.c prefilter convolution binds one of these per dispatch.</summary>
    public GpuDescriptorHandle CreateUnorderedAccessViewForCubeMipSlice(D3D12Texture cubemap, ID3D12DescriptorHeap* heap, uint slotIndex, uint mipSlice)
    {
        var uavDesc = new UnorderedAccessViewDesc
        {
            Format = cubemap.DxgiFormat,
            ViewDimension = UavDimension.Texture2Darray,
        };
        uavDesc.Anonymous.Texture2DArray = new Tex2DArrayUav
        {
            MipSlice = mipSlice,
            FirstArraySlice = 0,
            ArraySize = 6,
            PlaneSlice = 0,
        };

        return WriteUnorderedAccessView(cubemap.Native, &uavDesc, heap, slotIndex);
    }

    private GpuDescriptorHandle WriteShaderResourceView(ID3D12Resource* resource, ShaderResourceViewDesc* desc,
        ID3D12DescriptorHeap* heap, uint slotIndex)
    {
        var cpu = CpuSlot(_device, heap, DescriptorHeapType.CbvSrvUav, slotIndex);
        _device->CreateShaderResourceView(resource, desc, cpu);
        return GpuSlot(_device, heap, DescriptorHeapType.CbvSrvUav, slotIndex);
    }

    private GpuDescriptorHandle WriteUnorderedAccessView(ID3D12Resource* resource, UnorderedAccessViewDesc* desc,
        ID3D12DescriptorHeap* heap, uint slotIndex)
    {
        var cpu = CpuSlot(_device, heap, DescriptorHeapType.CbvSrvUav, slotIndex);
        _device->CreateUnorderedAccessView(resource, pCounterResource: null, desc, cpu);
        return GpuSlot(_device, heap, DescriptorHeapType.CbvSrvUav, slotIndex);
    }
}
