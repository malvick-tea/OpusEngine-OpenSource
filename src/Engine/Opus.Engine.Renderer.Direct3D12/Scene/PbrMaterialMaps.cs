using System;
using System.Collections.Generic;
using Opus.Content.Textures;
using Opus.Engine.Rhi;
using Opus.Engine.Rhi.Direct3D12;
using Silk.NET.Direct3D12;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>Shared GPU plumbing for the metal-roughness material atlases. Every material binds a
/// contiguous five-SRV run — base colour, normal, metallic-roughness, occlusion, emissive — so the
/// one forward PBR pixel shader samples a fixed register layout (<c>t0..t4</c>) regardless of which
/// <see cref="IMaterialAtlas"/> built the run. Holds the two neutral 1×1 fallback texels every atlas
/// substitutes for an absent map (white = base/metallic-roughness/occlusion identity, flat normal =
/// no perturbation) plus the run-writer that lays the five descriptors into a heap.</summary>
internal static unsafe class PbrMaterialMaps
{
    /// <summary>SRV descriptors per material run: base, normal, metallic-roughness, occlusion,
    /// emissive — matching the <c>t0..t4</c> table the forward PBR root signature declares.</summary>
    public const uint MapsPerMaterial = 5u;

    private static readonly byte[] WhiteTexel = { 255, 255, 255, 255 };

    // (128,128,255) decodes through the shader's *2-1 unpack to ~(0,0,1): a flat tangent-space
    // normal, i.e. "use the geometric normal unchanged" when a material binds no normal map.
    private static readonly byte[] FlatNormalTexel = { 128, 128, 255, 255 };

    public static D3D12Texture CreateWhite(D3D12RhiDevice device, D3D12CommandList cmd, string name, List<D3D12Buffer> staging)
        => CreateTexel(device, cmd, name, WhiteTexel, staging);

    public static D3D12Texture CreateFlatNormal(D3D12RhiDevice device, D3D12CommandList cmd, string name, List<D3D12Buffer> staging)
        => CreateTexel(device, cmd, name, FlatNormalTexel, staging);

    /// <summary>Creates a sampled texture from a decoded image, expanded to a full box-filtered
    /// mip chain and scheduled for upload on <paramref name="cmd"/>: a high-resolution material
    /// map rendered small needs the minified levels for the anisotropic sampler, or it aliases
    /// into shimmer. Shared by every atlas builder that uploads real images.</summary>
    public static D3D12Texture CreateMippedTexture(
        D3D12RhiDevice device,
        D3D12CommandList cmd,
        string name,
        DecodedImage decoded,
        List<D3D12Buffer> staging)
    {
        var mipChain = MipChain.Generate(decoded);
        var texture = device.CreateGraphicsTexture(new RhiTextureDescription(
            name, decoded.Width, decoded.Height, MipLevels: mipChain.Count,
            Format: RhiTextureFormat.Rgba8Unorm, Usage: RhiTextureUsage.Sampled));
        staging.Add(device.ScheduleMippedTextureUpload(texture, ToMipMemory(mipChain), cmd));
        return texture;
    }

    /// <summary>Creates a sampled texture from an already block-compressed mip chain (BC7 / BC5) and
    /// schedules its upload. The compressed blobs come from <see cref="BcnTextureEncoder"/> via the
    /// on-disk cache; <paramref name="gpuFormat"/> carries the sRGB-vs-linear view the encoder is
    /// agnostic to. Block layout is transparent to the upload path — the device derives the block row
    /// pitch from the format through <c>GetCopyableFootprints</c>, exactly as for an uncompressed
    /// texture.</summary>
    public static D3D12Texture CreateCompressedTexture(
        D3D12RhiDevice device,
        D3D12CommandList cmd,
        string name,
        CompressedTexture compressed,
        RhiTextureFormat gpuFormat,
        List<D3D12Buffer> staging)
    {
        var levels = new ReadOnlyMemory<byte>[compressed.MipBlocks.Count];
        for (var level = 0; level < compressed.MipBlocks.Count; level++)
        {
            levels[level] = compressed.MipBlocks[level];
        }

        var texture = device.CreateGraphicsTexture(new RhiTextureDescription(
            name, compressed.Width, compressed.Height, MipLevels: compressed.MipBlocks.Count,
            Format: gpuFormat, Usage: RhiTextureUsage.Sampled));
        staging.Add(device.ScheduleMippedTextureUpload(texture, levels, cmd));
        return texture;
    }

    private static IReadOnlyList<ReadOnlyMemory<byte>> ToMipMemory(IReadOnlyList<DecodedImage> mipChain)
    {
        var levels = new ReadOnlyMemory<byte>[mipChain.Count];
        for (var i = 0; i < mipChain.Count; i++)
        {
            levels[i] = mipChain[i].Rgba;
        }

        return levels;
    }

    /// <summary>Writes a material's five SRVs into <paramref name="heap"/> at the contiguous run
    /// starting <c>runIndex * MapsPerMaterial</c> and returns the GPU handle of the run's first
    /// descriptor — the table base the pass binds for that material. Callers pass the resolved
    /// texture for each map, substituting a neutral fallback for absent ones.</summary>
    public static GpuDescriptorHandle WriteRun(
        D3D12RhiDevice device,
        ID3D12DescriptorHeap* heap,
        uint runIndex,
        D3D12Texture baseColor,
        D3D12Texture normal,
        D3D12Texture metallicRoughness,
        D3D12Texture occlusion,
        D3D12Texture emissive)
    {
        var first = runIndex * MapsPerMaterial;
        var table = device.CreateShaderResourceView(baseColor, heap, slotIndex: first);
        device.CreateShaderResourceView(normal, heap, slotIndex: first + 1u);
        device.CreateShaderResourceView(metallicRoughness, heap, slotIndex: first + 2u);
        device.CreateShaderResourceView(occlusion, heap, slotIndex: first + 3u);
        device.CreateShaderResourceView(emissive, heap, slotIndex: first + 4u);
        return table;
    }

    private static D3D12Texture CreateTexel(
        D3D12RhiDevice device,
        D3D12CommandList cmd,
        string name,
        byte[] rgba,
        List<D3D12Buffer> staging)
    {
        var texture = device.CreateGraphicsTexture(new RhiTextureDescription(
            name, Width: 1, Height: 1, MipLevels: 1, Format: RhiTextureFormat.Rgba8Unorm, Usage: RhiTextureUsage.Sampled));
        staging.Add(device.ScheduleMippedTextureUpload(texture, new ReadOnlyMemory<byte>[] { rgba }, cmd));
        return texture;
    }
}
