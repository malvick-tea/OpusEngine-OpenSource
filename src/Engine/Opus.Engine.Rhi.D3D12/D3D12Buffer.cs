using System;
using Opus.Engine.Rhi;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>
/// D3D12 buffer resource backed by an upload-heap committed resource — CPU-mappable
/// directly. Phase R-1.4.a only ships this variant; default-heap buffers with a
/// staging copy land in R-1.4.b alongside textures.
///
/// M5 also uses this wrapper for READBACK heap buffers; those are CPU-readable
/// after a GPU copy and queue drain.
///
/// Lifetime: caller disposes after <c>WaitForIdle</c> / fence drain so the resource
/// release isn't racing in-flight GPU consumption.
/// </summary>
public sealed unsafe class D3D12Buffer : IRhiBuffer
{
    private ID3D12Resource* _resource;
    private bool _disposed;

    internal D3D12Buffer(string debugName, int sizeBytes, RhiBufferUsage usage, ID3D12Resource* resource)
    {
        DebugName = debugName;
        SizeBytes = sizeBytes;
        Usage = usage;
        _resource = resource;
    }

    public string DebugName { get; }

    public int SizeBytes { get; }

    public RhiBufferUsage Usage { get; }

    public ID3D12Resource* Native => _resource;

    /// <summary>GPU-side virtual address — what <c>D3D12_VERTEX_BUFFER_VIEW</c>
    /// /<c>D3D12_INDEX_BUFFER_VIEW</c> consume for IA binding.</summary>
    public ulong GpuVirtualAddress => _resource->GetGPUVirtualAddress();

    /// <summary>Maps + memcpys + unmaps. Valid only on upload-heap buffers (Phase R-1.4.a
    /// has no other kind). For larger streamed updates a persistent map + ring buffer
    /// pattern lands later when the renderer needs per-frame uniform updates.</summary>
    public void Upload(ReadOnlySpan<byte> data)
    {
        if (data.Length > SizeBytes)
        {
            throw new ArgumentException($"data ({data.Length} bytes) exceeds buffer size ({SizeBytes} bytes)", nameof(data));
        }

        // Read-range Begin == End signals "we won't read mapped contents back" — important
        // optimisation hint on integrated GPUs and remote-memory architectures.
        var readRange = new Silk.NET.Direct3D12.Range { Begin = 0, End = 0 };
        void* mapped = null;
        SilkMarshal.ThrowHResult(_resource->Map(0u, &readRange, &mapped));
        try
        {
            data.CopyTo(new Span<byte>(mapped, SizeBytes));
        }
        finally
        {
            _resource->Unmap(0u, pWrittenRange: null);
        }
    }

    /// <summary>Maps a readback buffer and copies its contents into <paramref name="destination"/>.
    /// Caller must drain the queue before invoking this after a GPU copy.</summary>
    public void Read(Span<byte> destination)
    {
        if (destination.Length > SizeBytes)
        {
            throw new ArgumentException(
                $"destination ({destination.Length} bytes) exceeds buffer size ({SizeBytes} bytes)",
                nameof(destination));
        }

        var readRange = new Silk.NET.Direct3D12.Range { Begin = 0, End = (nuint)destination.Length };
        void* mapped = null;
        SilkMarshal.ThrowHResult(_resource->Map(0u, &readRange, &mapped));
        try
        {
            new Span<byte>(mapped, SizeBytes).Slice(0, destination.Length).CopyTo(destination);
        }
        finally
        {
            var writtenRange = new Silk.NET.Direct3D12.Range { Begin = 0, End = 0 };
            _resource->Unmap(0u, &writtenRange);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_resource != null)
        {
            _resource->Release();
            _resource = null;
        }

        _disposed = true;
    }
}
