using System;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Buffer creation. UAV buffers go on the DEFAULT heap (GPU-local);
/// readback buffers go on READBACK; vertex / index / uniform / structured-readonly
/// buffers go on UPLOAD (CPU-mappable).</summary>
public sealed unsafe partial class D3D12RhiDevice
{
    public IRhiBuffer CreateBuffer(RhiBufferDescription description) => CreateGraphicsBuffer(description);

    /// <summary>Backend-typed buffer factory — concrete return type exposes
    /// <see cref="D3D12Buffer.Upload"/> / <see cref="D3D12Buffer.GpuVirtualAddress"/>.</summary>
    public D3D12Buffer CreateGraphicsBuffer(RhiBufferDescription description)
    {
        if (description.SizeBytes <= 0)
        {
            throw new ArgumentException("SizeBytes must be > 0", nameof(description));
        }

        var isUav = (description.Usage & RhiBufferUsage.UnorderedAccess) != 0;
        var isReadback = (description.Usage & RhiBufferUsage.Readback) != 0;
        if (isUav && isReadback)
        {
            throw new ArgumentException("A D3D12 buffer cannot be both UAV and readback.", nameof(description));
        }

        var heapProps = new HeapProperties
        {
            Type = isReadback ? HeapType.Readback : isUav ? HeapType.Default : HeapType.Upload,
            CPUPageProperty = CpuPageProperty.Unknown,
            MemoryPoolPreference = MemoryPool.Unknown,
            CreationNodeMask = 1,
            VisibleNodeMask = 1,
        };

        var resourceDesc = new ResourceDesc
        {
            Dimension = ResourceDimension.Buffer,
            Alignment = 0,
            Width = (ulong)description.SizeBytes,
            Height = 1,
            DepthOrArraySize = 1,
            MipLevels = 1,
            Format = Format.FormatUnknown,
            SampleDesc = new SampleDesc(1, 0),
            Layout = TextureLayout.LayoutRowMajor,
            Flags = isUav ? ResourceFlags.AllowUnorderedAccess : ResourceFlags.None,
        };

        // Upload heap requires GenericRead initial state — CPU writes + GPU reads.
        // Default-heap UAVs start in UnorderedAccess so a compute pass can write on first dispatch.
        var initialState = isReadback
            ? ResourceStates.CopyDest
            : isUav
                ? ResourceStates.UnorderedAccess
                : ResourceStates.GenericRead;

        ID3D12Resource* resource = null;
        var resourceGuid = ID3D12Resource.Guid;
        SilkMarshal.ThrowHResult(_device->CreateCommittedResource(
            &heapProps,
            HeapFlags.None,
            &resourceDesc,
            initialState,
            pOptimizedClearValue: null,
            &resourceGuid,
            (void**)&resource));

        return new D3D12Buffer(description.DebugName, description.SizeBytes, description.Usage, resource);
    }

    /// <summary>Creates a CPU-readable D3D12 readback buffer. Used by screenshot capture
    /// and GPU diagnostics that need a small, explicit copy out of a texture.</summary>
    public D3D12Buffer CreateReadbackBuffer(string debugName, int sizeBytes) =>
        CreateGraphicsBuffer(new RhiBufferDescription(debugName, sizeBytes, RhiBufferUsage.Readback));
}
