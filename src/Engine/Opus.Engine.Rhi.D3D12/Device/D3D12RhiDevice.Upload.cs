using System;
using System.Collections.Generic;
using Silk.NET.Direct3D12;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>CPU→GPU texture upload scheduler. Allocates a row-aligned staging buffer,
/// memcpys with the 256-byte row pitch D3D12 expects, records CopyTextureRegion +
/// CopyDest→PixelShaderResource transition. <see cref="ScheduleTextureUpload"/> handles a
/// single subresource; <see cref="ScheduleMippedTextureUpload"/> uploads a full mip chain
/// in one staging buffer so a trilinear / anisotropic sampler has every minified level.</summary>
public sealed unsafe partial class D3D12RhiDevice
{
    public D3D12Buffer ScheduleTextureUpload(D3D12Texture texture, ReadOnlySpan<byte> pixels, D3D12CommandList commandList)
    {
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentNullException.ThrowIfNull(commandList);

        var resourceDesc = texture.Native->GetDesc();
        PlacedSubresourceFootprint footprint;
        uint numRows;
        ulong rowSizeBytes;
        ulong totalBytes;
        _device->GetCopyableFootprints(
            &resourceDesc,
            FirstSubresource: 0u,
            NumSubresources: 1u,
            BaseOffset: 0,
            &footprint,
            &numRows,
            &rowSizeBytes,
            &totalBytes);

        var staging = CreateGraphicsBuffer(new RhiBufferDescription(
            $"{texture.DebugName}.staging", (int)totalBytes, RhiBufferUsage.Staging));

        var stagingBytes = new byte[totalBytes];
        CopyRows(pixels, stagingBytes, (int)rowSizeBytes, (int)footprint.Footprint.RowPitch, (int)numRows);
        staging.Upload(stagingBytes);

        commandList.CopyBufferToTexture(staging, footprint, texture);
        commandList.ResourceBarrierTransition(texture, ResourceStates.CopyDest, ResourceStates.PixelShaderResource);

        return staging;
    }

    /// <summary>Uploads every level of a mip chain into a single staging buffer, one
    /// <c>CopyTextureRegion</c> per subresource, then a single all-subresources transition
    /// to <see cref="ResourceStates.PixelShaderResource"/>. <paramref name="mipLevels"/>
    /// must hold exactly the texture's mip count, ordered level 0 (full size) first.</summary>
    public D3D12Buffer ScheduleMippedTextureUpload(
        D3D12Texture texture,
        IReadOnlyList<ReadOnlyMemory<byte>> mipLevels,
        D3D12CommandList commandList)
    {
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentNullException.ThrowIfNull(mipLevels);
        ArgumentNullException.ThrowIfNull(commandList);
        if (mipLevels.Count == 0)
        {
            throw new ArgumentException("At least one mip level is required.", nameof(mipLevels));
        }

        var mipCount = (uint)mipLevels.Count;
        var resourceDesc = texture.Native->GetDesc();
        var footprints = stackalloc PlacedSubresourceFootprint[(int)mipCount];
        var numRows = stackalloc uint[(int)mipCount];
        var rowSizes = stackalloc ulong[(int)mipCount];
        ulong totalBytes;
        _device->GetCopyableFootprints(
            &resourceDesc, 0u, mipCount, 0, footprints, numRows, rowSizes, &totalBytes);

        var staging = CreateGraphicsBuffer(new RhiBufferDescription(
            $"{texture.DebugName}.staging", (int)totalBytes, RhiBufferUsage.Staging));

        var stagingBytes = new byte[totalBytes];
        for (var mip = 0; mip < mipLevels.Count; mip++)
        {
            CopyRows(
                mipLevels[mip].Span,
                stagingBytes.AsSpan((int)footprints[mip].Offset),
                (int)rowSizes[mip],
                (int)footprints[mip].Footprint.RowPitch,
                (int)numRows[mip]);
        }

        staging.Upload(stagingBytes);

        for (var mip = 0u; mip < mipCount; mip++)
        {
            commandList.CopyBufferToTexture(staging, footprints[mip], texture, mip);
        }

        commandList.ResourceBarrierTransition(texture, ResourceStates.CopyDest, ResourceStates.PixelShaderResource);
        return staging;
    }

    /// <summary>Copies <paramref name="numRows"/> tightly-packed source rows into a
    /// staging span padded to the GPU's aligned row pitch (each D3D12 subresource row is
    /// 256-byte aligned, so the destination stride is wider than the source).</summary>
    private static void CopyRows(
        ReadOnlySpan<byte> source,
        Span<byte> destination,
        int sourceRowSize,
        int destinationRowPitch,
        int numRows)
    {
        for (var row = 0; row < numRows; row++)
        {
            source.Slice(row * sourceRowSize, sourceRowSize)
                .CopyTo(destination.Slice(row * destinationRowPitch, sourceRowSize));
        }
    }
}
