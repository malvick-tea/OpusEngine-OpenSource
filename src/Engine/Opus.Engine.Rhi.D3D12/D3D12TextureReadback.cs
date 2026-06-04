using System;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Reusable D3D12 texture readback target. Create it for a texture, record a
/// copy while the host's command list is open, drain the graphics queue, then read the
/// row-pitched bytes back as a tight RGBA8 screenshot.</summary>
public sealed unsafe class D3D12TextureReadback : IDisposable
{
    private const int BytesPerPixel = 4;

    private readonly D3D12Buffer _buffer;
    private readonly PlacedSubresourceFootprint _footprint;
    private readonly Format _format;
    private readonly int _rowSizeBytes;
    private bool _disposed;

    private D3D12TextureReadback(
        D3D12Buffer buffer,
        PlacedSubresourceFootprint footprint,
        Format format,
        int width,
        int height,
        int rowSizeBytes)
    {
        _buffer = buffer;
        _footprint = footprint;
        _format = format;
        Width = width;
        Height = height;
        _rowSizeBytes = rowSizeBytes;
    }

    public int Width { get; }

    public int Height { get; }

    public int RowPitch => (int)_footprint.Footprint.RowPitch;

    public static D3D12TextureReadback Create(D3D12RhiDevice device, D3D12Texture source, string debugName)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Create(device, source.Native, debugName);
    }

    public static D3D12TextureReadback CreateForCurrentBackBuffer(
        D3D12RhiDevice device,
        D3D12SwapChain swapChain,
        string debugName)
    {
        ArgumentNullException.ThrowIfNull(swapChain);
        return Create(device, swapChain.CurrentBackBuffer, debugName);
    }

    public static D3D12TextureReadback Create(D3D12RhiDevice device, ID3D12Resource* source, string debugName)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentException.ThrowIfNullOrWhiteSpace(debugName);
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        var desc = source->GetDesc();
        if (desc.Dimension != ResourceDimension.Texture2D)
        {
            throw new ArgumentException("Only Texture2D readback is supported.", nameof(source));
        }

        if (desc.DepthOrArraySize != 1 || desc.MipLevels != 1)
        {
            throw new ArgumentException("Only single-slice, single-mip readback is supported.", nameof(source));
        }

        ValidateScreenshotFormat(desc.Format);

        PlacedSubresourceFootprint footprint;
        uint numRows;
        ulong rowSizeBytes;
        ulong totalBytes;
        device.NativeDevice->GetCopyableFootprints(
            &desc,
            FirstSubresource: 0u,
            NumSubresources: 1u,
            BaseOffset: 0,
            &footprint,
            &numRows,
            &rowSizeBytes,
            &totalBytes);

        var width = checked((int)desc.Width);
        var height = checked((int)desc.Height);
        if (numRows != height)
        {
            throw new InvalidOperationException($"D3D12 reported {numRows} rows for a {height}px texture.");
        }

        var buffer = device.CreateReadbackBuffer($"{debugName}.readback", checked((int)totalBytes));
        return new D3D12TextureReadback(buffer, footprint, desc.Format, width, height, checked((int)rowSizeBytes));
    }

    public void RecordCopyFrom(
        D3D12CommandList commandList,
        D3D12Texture source,
        ResourceStates currentState,
        ResourceStates finalState)
    {
        ArgumentNullException.ThrowIfNull(source);
        RecordCopyFrom(commandList, source.Native, currentState, finalState);
    }

    public void RecordCopyFrom(
        D3D12CommandList commandList,
        ID3D12Resource* source,
        ResourceStates currentState,
        ResourceStates finalState)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(commandList);
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        ValidateSourceMatches(source);
        TransitionIfNeeded(commandList, source, currentState, ResourceStates.CopySource);
        commandList.CopyTextureToBuffer(source, _buffer, _footprint);
        TransitionIfNeeded(commandList, source, ResourceStates.CopySource, finalState);
    }

    public D3D12Screenshot ReadRgba8()
    {
        ThrowIfDisposed();
        var rowPitch = RowPitch;
        var staged = new byte[_buffer.SizeBytes];
        _buffer.Read(staged);

        var pixels = new byte[checked(Width * Height * BytesPerPixel)];
        var sourceOffset = checked((int)_footprint.Offset);
        for (var y = 0; y < Height; y++)
        {
            var sourceRow = staged.AsSpan(sourceOffset + (y * rowPitch), _rowSizeBytes);
            var destRow = pixels.AsSpan(y * Width * BytesPerPixel, Width * BytesPerPixel);
            if (IsBgra(_format))
            {
                CopyBgraToRgba(sourceRow, destRow, Width);
            }
            else
            {
                sourceRow.Slice(0, destRow.Length).CopyTo(destRow);
            }
        }

        return new D3D12Screenshot(Width, Height, pixels, _format.ToString(), rowPitch);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _buffer.Dispose();
        _disposed = true;
    }

    private static void TransitionIfNeeded(
        D3D12CommandList commandList,
        ID3D12Resource* resource,
        ResourceStates before,
        ResourceStates after)
    {
        if (before == after)
        {
            return;
        }

        commandList.ResourceBarrierTransition(resource, before, after);
    }

    private static void CopyBgraToRgba(ReadOnlySpan<byte> source, Span<byte> destination, int width)
    {
        for (var x = 0; x < width; x++)
        {
            var i = x * BytesPerPixel;
            destination[i + 0] = source[i + 2];
            destination[i + 1] = source[i + 1];
            destination[i + 2] = source[i + 0];
            destination[i + 3] = source[i + 3];
        }
    }

    private static void ValidateScreenshotFormat(Format format)
    {
        if (format is not (Format.FormatR8G8B8A8Unorm or Format.FormatB8G8R8A8Unorm))
        {
            throw new NotSupportedException($"D3D12 screenshot readback supports RGBA8/BGRA8 only, not {format}.");
        }
    }

    private static bool IsBgra(Format format) => format == Format.FormatB8G8R8A8Unorm;

    private void ValidateSourceMatches(ID3D12Resource* source)
    {
        var desc = source->GetDesc();
        if (desc.Width != (ulong)Width || desc.Height != (uint)Height || desc.Format != _format)
        {
            throw new ArgumentException(
                $"Readback target was created for {Width}x{Height} {_format}, but source is {desc.Width}x{desc.Height} {desc.Format}.",
                nameof(source));
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
