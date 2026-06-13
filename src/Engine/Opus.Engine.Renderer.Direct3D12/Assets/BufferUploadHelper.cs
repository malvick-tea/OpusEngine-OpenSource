using System;
using System.Runtime.InteropServices;
using Opus.Engine.Rhi.Direct3D12;

namespace Opus.Engine.Renderer.Direct3D12.Assets;

/// <summary>Tiny internal helper for uploading typed arrays into <see cref="D3D12Buffer"/>
/// staging slots. Mirrors the test-side <c>BufferUpload</c> helper but stays in the
/// production layer so <see cref="D3D12GltfSceneLoader"/> doesn't reach across to test
/// fixtures. When the abstract <c>IRhiBuffer</c> upload surface lands, this collapses
/// into one call on the abstract device.</summary>
internal static class BufferUploadHelper
{
    /// <summary>Writes <paramref name="data"/> into <paramref name="buffer"/> as raw
    /// bytes via <see cref="MemoryMarshal.AsBytes{T}"/>. The buffer must already be sized
    /// at least <c>data.Length * sizeof(T)</c> bytes.</summary>
    public static void WriteStructs<T>(D3D12Buffer buffer, T[] data)
        where T : unmanaged
        => buffer.Upload(MemoryMarshal.AsBytes(data.AsSpan()));
}
