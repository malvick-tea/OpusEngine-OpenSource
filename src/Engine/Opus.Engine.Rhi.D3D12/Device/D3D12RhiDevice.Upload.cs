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
    private const int MaxTextureMipLevels = 32;

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

        var stagingByteCount = CheckedNativeSize(totalBytes, "texture staging buffer");
        var sourceRowSize = CheckedNativeSize(rowSizeBytes, "texture row");
        var rowCount = checked((int)numRows);
        var rowPitch = checked((int)footprint.Footprint.RowPitch);
        var staging = CreateGraphicsBuffer(new RhiBufferDescription(
            $"{texture.DebugName}.staging", stagingByteCount, RhiBufferUsage.Staging));

        var stagingBytes = new byte[stagingByteCount];
        CopyRows(pixels, stagingBytes, sourceRowSize, rowPitch, rowCount);
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

        if (mipLevels.Count > MaxTextureMipLevels)
        {
            throw new ArgumentOutOfRangeException(
                nameof(mipLevels),
                mipLevels.Count,
                $"Texture uploads support at most {MaxTextureMipLevels} mip levels.");
        }

        var mipCount = (uint)mipLevels.Count;
        var resourceDesc = texture.Native->GetDesc();
        if (mipCount > resourceDesc.MipLevels)
        {
            throw new ArgumentException(
                $"Upload contains {mipCount} mip levels but texture '{texture.DebugName}' has {resourceDesc.MipLevels}.",
                nameof(mipLevels));
        }

        var footprints = stackalloc PlacedSubresourceFootprint[(int)mipCount];
        var numRows = stackalloc uint[(int)mipCount];
        var rowSizes = stackalloc ulong[(int)mipCount];
        ulong totalBytes;
        _device->GetCopyableFootprints(
            &resourceDesc, 0u, mipCount, 0, footprints, numRows, rowSizes, &totalBytes);

        var stagingByteCount = CheckedNativeSize(totalBytes, "mipped texture staging buffer");
        var staging = CreateGraphicsBuffer(new RhiBufferDescription(
            $"{texture.DebugName}.staging", stagingByteCount, RhiBufferUsage.Staging));

        var stagingBytes = new byte[stagingByteCount];
        for (var mip = 0; mip < mipLevels.Count; mip++)
        {
            var offset = CheckedNativeSize(footprints[mip].Offset, "mip staging offset");
            var rowSize = CheckedNativeSize(rowSizes[mip], "mip row");
            var rowPitch = checked((int)footprints[mip].Footprint.RowPitch);
            var rowCount = checked((int)numRows[mip]);
            CopyRows(
                mipLevels[mip].Span,
                stagingBytes.AsSpan(offset),
                rowSize,
                rowPitch,
                rowCount);
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
        var requiredSourceBytes = checked(sourceRowSize * numRows);
        var requiredDestinationBytes = numRows == 0
            ? 0
            : checked(((numRows - 1) * destinationRowPitch) + sourceRowSize);
        if (source.Length != requiredSourceBytes)
        {
            throw new ArgumentException(
                $"Texture source contains {source.Length} bytes; expected {requiredSourceBytes}.",
                nameof(source));
        }

        if (destination.Length < requiredDestinationBytes)
        {
            throw new ArgumentException(
                $"Texture staging destination contains {destination.Length} bytes; expected at least {requiredDestinationBytes}.",
                nameof(destination));
        }

        for (var row = 0; row < numRows; row++)
        {
            source.Slice(row * sourceRowSize, sourceRowSize)
                .CopyTo(destination.Slice(row * destinationRowPitch, sourceRowSize));
        }
    }

    private static int CheckedNativeSize(ulong value, string label)
    {
        if (value > int.MaxValue)
        {
            throw new InvalidOperationException(
                $"{label} requires {value} bytes, exceeding the managed upload limit.");
        }

        return (int)value;
    }
}
