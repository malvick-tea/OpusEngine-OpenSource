using System;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Texture creation. 2D / cubemap (with + without mip chain) / 2D depth array.</summary>
public sealed unsafe partial class D3D12RhiDevice
{
    private static readonly HeapProperties DefaultHeapProps = new()
    {
        Type = HeapType.Default,
        CPUPageProperty = CpuPageProperty.Unknown,
        MemoryPoolPreference = MemoryPool.Unknown,
        CreationNodeMask = 1,
        VisibleNodeMask = 1,
    };

    public IRhiTexture CreateTexture(RhiTextureDescription description) => CreateGraphicsTexture(description);

    /// <summary>2D texture factory. State follows usage: depth→DepthWrite,
    /// colour-target→RenderTarget, UAV-only→UnorderedAccess, sampled→CopyDest (caller uploads).</summary>
    public D3D12Texture CreateGraphicsTexture(RhiTextureDescription description)
    {
        if (description.Width <= 0 || description.Height <= 0)
        {
            throw new ArgumentException("Width and Height must be > 0", nameof(description));
        }

        var dxgiFormat = DxgiFormatMap.Resolve(description.Format);
        var mips = description.MipLevels > 0 ? (ushort)description.MipLevels : (ushort)1;
        var isDepth = (description.Usage & RhiTextureUsage.DepthStencilTarget) != 0;
        var isUav = (description.Usage & RhiTextureUsage.UnorderedAccess) != 0;
        var isColorTarget = (description.Usage & RhiTextureUsage.ColorTarget) != 0;

        var flags = ResourceFlags.None;
        if (isDepth)
        {
            flags |= ResourceFlags.AllowDepthStencil;
        }

        if (isUav)
        {
            flags |= ResourceFlags.AllowUnorderedAccess;
        }

        if (isColorTarget)
        {
            flags |= ResourceFlags.AllowRenderTarget;
        }

        var resourceDesc = new ResourceDesc
        {
            Dimension = ResourceDimension.Texture2D,
            Alignment = 0,
            Width = (ulong)description.Width,
            Height = (uint)description.Height,
            DepthOrArraySize = 1,
            MipLevels = mips,
            Format = dxgiFormat,
            SampleDesc = new SampleDesc(1, 0),
            Layout = TextureLayout.LayoutUnknown,
            Flags = flags,
        };

        var initialState = isDepth
            ? ResourceStates.DepthWrite
            : isColorTarget
                ? ResourceStates.RenderTarget
                : isUav
                    ? ResourceStates.UnorderedAccess
                    : ResourceStates.CopyDest;

        ClearValue clearValue;
        ClearValue* optimisedClear = null;
        if (isDepth)
        {
            clearValue = new ClearValue { Format = dxgiFormat };
            clearValue.Anonymous.DepthStencil = new DepthStencilValue { Depth = 1.0f, Stencil = 0 };
            optimisedClear = &clearValue;
        }
        else if (isColorTarget)
        {
            clearValue = new ClearValue { Format = dxgiFormat };
            clearValue.Anonymous.Color[0] = 0f;
            clearValue.Anonymous.Color[1] = 0f;
            clearValue.Anonymous.Color[2] = 0f;
            clearValue.Anonymous.Color[3] = 1f;
            optimisedClear = &clearValue;
        }

        var resource = CommitResource(in resourceDesc, initialState, optimisedClear);
        return new D3D12Texture(description.DebugName, description.Width, description.Height, mips,
            description.Format, description.Usage, dxgiFormat, resource);
    }

    /// <summary>6-slice 2D texture suitable as a cubemap. Cubemap-ness is purely an SRV
    /// interpretation in D3D12, so the resource is a 2D array with DepthOrArraySize = 6.</summary>
    public D3D12Texture CreateGraphicsColorTextureCube(string debugName, int faceSize, RhiTextureFormat format, bool allowUnorderedAccess = true)
    {
        if (faceSize <= 0)
        {
            throw new ArgumentException("faceSize must be > 0");
        }

        var dxgiFormat = DxgiFormatMap.Resolve(format);
        var resourceDesc = CubeResourceDesc(faceSize, mipCount: 1, dxgiFormat,
            allowUnorderedAccess ? ResourceFlags.AllowUnorderedAccess : ResourceFlags.None);
        var initialState = allowUnorderedAccess ? ResourceStates.UnorderedAccess : ResourceStates.CopyDest;

        var resource = CommitResource(in resourceDesc, initialState, optimisedClear: null);
        var usage = RhiTextureUsage.Sampled | (allowUnorderedAccess ? RhiTextureUsage.UnorderedAccess : RhiTextureUsage.None);
        return new D3D12Texture(debugName, faceSize, faceSize, 1, format, usage, dxgiFormat, resource,
            ownsNative: true, arraySize: 6);
    }

    /// <summary>R-9.c prefiltered specular cubemap with a mip chain — each mip holds the
    /// env cubemap convolved at a different roughness level.</summary>
    public D3D12Texture CreateGraphicsColorTextureCubeWithMips(string debugName, int faceSize, int mipCount, RhiTextureFormat format)
    {
        if (faceSize <= 0 || mipCount <= 0)
        {
            throw new ArgumentException("faceSize / mipCount must be > 0");
        }

        var dxgiFormat = DxgiFormatMap.Resolve(format);
        var resourceDesc = CubeResourceDesc(faceSize, mipCount, dxgiFormat, ResourceFlags.AllowUnorderedAccess);

        var resource = CommitResource(in resourceDesc, ResourceStates.UnorderedAccess, optimisedClear: null);
        return new D3D12Texture(debugName, faceSize, faceSize, mipCount, format,
            RhiTextureUsage.Sampled | RhiTextureUsage.UnorderedAccess, dxgiFormat, resource,
            ownsNative: true, arraySize: 6);
    }

    /// <summary>2D depth-stencil array. Each slice is independently renderable via
    /// <see cref="CreateDepthStencilViewForSlice"/> and sampleable as part of the whole array via
    /// <see cref="CreateDepthShaderResourceViewForArray"/>. Used by R-7.b cascaded shadow maps.</summary>
    public D3D12Texture CreateGraphicsDepthTextureArray(string debugName, int width, int height, int arraySize)
    {
        if (width <= 0 || height <= 0 || arraySize <= 0)
        {
            throw new ArgumentException("width / height / arraySize must be > 0");
        }

        var dxgiFormat = Format.FormatD32Float;
        var resourceDesc = new ResourceDesc
        {
            Dimension = ResourceDimension.Texture2D,
            Alignment = 0,
            Width = (ulong)width,
            Height = (uint)height,
            DepthOrArraySize = (ushort)arraySize,
            MipLevels = 1,
            Format = dxgiFormat,
            SampleDesc = new SampleDesc(1, 0),
            Layout = TextureLayout.LayoutUnknown,
            Flags = ResourceFlags.AllowDepthStencil,
        };

        var depthClear = new ClearValue { Format = dxgiFormat };
        depthClear.Anonymous.DepthStencil = new DepthStencilValue { Depth = 1.0f, Stencil = 0 };

        var resource = CommitResource(in resourceDesc, ResourceStates.DepthWrite, &depthClear);
        return new D3D12Texture(debugName, width, height, 1, RhiTextureFormat.D32Float,
            RhiTextureUsage.DepthStencilTarget | RhiTextureUsage.Sampled, dxgiFormat, resource,
            ownsNative: true, arraySize: arraySize);
    }

    private static ResourceDesc CubeResourceDesc(int faceSize, int mipCount, Format format, ResourceFlags flags) =>
        new()
        {
            Dimension = ResourceDimension.Texture2D,
            Alignment = 0,
            Width = (ulong)faceSize,
            Height = (uint)faceSize,
            DepthOrArraySize = 6,
            MipLevels = (ushort)mipCount,
            Format = format,
            SampleDesc = new SampleDesc(1, 0),
            Layout = TextureLayout.LayoutUnknown,
            Flags = flags,
        };

    private ID3D12Resource* CommitResource(in ResourceDesc desc, ResourceStates initialState, ClearValue* optimisedClear)
    {
        ID3D12Resource* resource = null;
        var resourceGuid = ID3D12Resource.Guid;
        var heapProps = DefaultHeapProps;
        fixed (ResourceDesc* pDesc = &desc)
        {
            SilkMarshal.ThrowHResult(_device->CreateCommittedResource(
                &heapProps, HeapFlags.None, pDesc, initialState, optimisedClear, &resourceGuid, (void**)&resource));
        }

        return resource;
    }
}
