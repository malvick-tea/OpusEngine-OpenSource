using System;
using Silk.NET.DXGI;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>RHI texture format → DXGI format mapping. Single source of truth, used by every
/// texture / view creation method in the device.</summary>
internal static class DxgiFormatMap
{
    public static Format Resolve(RhiTextureFormat format) => format switch
    {
        RhiTextureFormat.Rgba8Unorm => Format.FormatR8G8B8A8Unorm,
        RhiTextureFormat.Bgra8UnormSrgb => Format.FormatB8G8R8A8UnormSrgb,
        RhiTextureFormat.Rgba16Float => Format.FormatR16G16B16A16Float,
        RhiTextureFormat.Rg16Float => Format.FormatR16G16Float,
        RhiTextureFormat.R32Float => Format.FormatR32Float,
        RhiTextureFormat.R8Unorm => Format.FormatR8Unorm,
        RhiTextureFormat.D32Float => Format.FormatD32Float,
        RhiTextureFormat.D24UnormS8Uint => Format.FormatD24UnormS8Uint,
        RhiTextureFormat.Bc7UnormSrgb => Format.FormatBC7UnormSrgb,
        RhiTextureFormat.Bc7Unorm => Format.FormatBC7Unorm,
        RhiTextureFormat.Bc5Unorm => Format.FormatBC5Unorm,
        _ => throw new ArgumentException($"Unsupported texture format: {format}", nameof(format)),
    };
}
