using System;
using Opus.Engine.Rhi;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>
/// D3D12 2D texture resource — DEFAULT heap (GPU-local), backed by a committed resource.
/// Initial state is <c>CopyDest</c> so the upload path can blit a staging buffer in
/// before the first draw transitions it to <c>PixelShaderResource</c>.
///
/// R-1.4.b ships sampled 2D textures only. R-3 generalises for render targets / UAVs /
/// depth-stencils / texture arrays / cubemaps as the renderer needs them.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "MA0055:Do not use finalizer",
    Justification = "The finalizer releases only an owned unmanaged COM reference.")]
public sealed unsafe class D3D12Texture : IRhiTexture
{
    private ID3D12Resource* _resource;
    private readonly bool _ownsNative;
    private bool _disposed;

    internal D3D12Texture(
        string debugName,
        int width,
        int height,
        int mipLevels,
        RhiTextureFormat format,
        RhiTextureUsage usage,
        Format dxgiFormat,
        ID3D12Resource* resource,
        bool ownsNative = true,
        int arraySize = 1)
    {
        DebugName = debugName;
        Width = width;
        Height = height;
        MipLevels = mipLevels;
        Format = format;
        Usage = usage;
        DxgiFormat = dxgiFormat;
        ArraySize = arraySize;
        _resource = resource;
        _ownsNative = ownsNative;
    }

    public int ArraySize { get; }

    /// <summary>Wraps a native <see cref="ID3D12Resource"/> the caller already owns (typical
    /// case: a swap-chain back buffer or a render target the frame graph imports). The
    /// returned wrapper does NOT call <c>Release</c> on dispose — caller keeps the native
    /// lifetime responsibility.</summary>
    public static D3D12Texture WrapNonOwning(
        string debugName,
        ID3D12Resource* native,
        int width,
        int height,
        Format dxgiFormat,
        RhiTextureFormat abstractFormat = RhiTextureFormat.Rgba8Unorm,
        RhiTextureUsage usage = RhiTextureUsage.ColorTarget) =>
        new(debugName, width, height, 1, abstractFormat, usage, dxgiFormat, native, ownsNative: false);

    public string DebugName { get; }

    public int Width { get; }

    public int Height { get; }

    public int MipLevels { get; }

    public RhiTextureFormat Format { get; }

    public RhiTextureUsage Usage { get; }

    public Format DxgiFormat { get; }

    public ID3D12Resource* Native => _resource;

    ~D3D12Texture()
    {
        ReleaseNative();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ReleaseNative();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void ReleaseNative()
    {
        if (_ownsNative && _resource != null)
        {
            _resource->Release();
        }

        _resource = null;
    }
}
