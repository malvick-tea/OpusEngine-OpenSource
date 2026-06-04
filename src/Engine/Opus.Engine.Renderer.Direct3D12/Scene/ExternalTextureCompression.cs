using System;
using Opus.Content.Textures;
using Opus.Engine.Rhi;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>Maps an authored PBR map kind to the CPU block-compression format the encoder targets
/// and the matching GPU texture format the SRV is created with. Base colour and emissive carry sRGB
/// colour, so they compress as BC7 and sample through an sRGB view; the packed ORM is linear data
/// (BC7 linear); tangent-space normals compress as BC5 (two channels, the shader reconstructs Z).
/// The split exists because the encoder is colour-space-agnostic — sRGB vs linear is purely the GPU
/// view — so the kind is the single place both decisions are made.</summary>
public static class ExternalTextureCompression
{
    public static (BlockCompressionFormat Encoder, RhiTextureFormat Gpu) For(ExternalMaterialMapKind kind) => kind switch
    {
        ExternalMaterialMapKind.BaseColor => (BlockCompressionFormat.Bc7, RhiTextureFormat.Bc7UnormSrgb),
        ExternalMaterialMapKind.Emissive => (BlockCompressionFormat.Bc7, RhiTextureFormat.Bc7UnormSrgb),
        ExternalMaterialMapKind.Normal => (BlockCompressionFormat.Bc5, RhiTextureFormat.Bc5Unorm),
        ExternalMaterialMapKind.Orm => (BlockCompressionFormat.Bc7, RhiTextureFormat.Bc7Unorm),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown external material map kind."),
    };
}
