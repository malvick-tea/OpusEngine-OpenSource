using System;
using System.Collections.Generic;
using System.IO;
using Opus.Content.Meshes;
using Opus.Content.Textures;
using Opus.Engine.Rhi.Direct3D12;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>Assembles a <see cref="MultiMaterialAtlas"/> from a raw GLB byte stream.
/// Composes the existing glTF parsing helpers (<see cref="GltfImageReader.ReadMaterialBindings"/>
/// for spec-gloss-aware base-colour resolution and <see cref="GltfImageReader.ReadEmbeddedImagesByIndex"/>
/// for image-blob lookup) with the device's texture-upload helpers — so the loader's
/// only responsibility is "give me an atlas for these bytes".
/// <para>
/// Every decoded image is expanded to a full <see cref="MipChain"/> and uploaded with all
/// levels: a high-resolution camo texture rendered small needs the minified levels for the
/// anisotropic sampler, or it aliases into shimmering noise that reads as a washed-out
/// flat surface.
/// </para>
/// <para>
/// On-failure-decode for an image returns a 2×2 magenta texel: a visible signal in
/// renders that the image's bytes couldn't be parsed, without taking the whole load
/// path down. Materials whose binding fully misses (no image / out-of-range / external
/// URI) silently route through the atlas's fallback white slot.
/// </para></summary>
public static class MultiMaterialAtlasBuilder
{
    private const int MagentaErrorExtent = 2;

    /// <summary>Magenta is unmistakable in normal scenes — used when a single image's
    /// bytes can't be decoded so the bad asset surfaces visually instead of crashing
    /// the load.</summary>
    private static readonly byte[] MagentaErrorRgba = new byte[]
    {
        255, 0, 255, 255,
        255, 0, 255, 255,
        255, 0, 255, 255,
        255, 0, 255, 255,
    };

    public static MultiMaterialAtlas BuildFromGlb(
        D3D12RhiDevice device,
        ReadOnlySpan<byte> glbBytes,
        string namePrefix)
    {
        ArgumentNullException.ThrowIfNull(device);

        var bindings = GltfImageReader.ReadMaterialBindings(glbBytes);
        var layout = MultiMaterialAtlasPlan.Build(bindings);
        var images = GltfImageReader.ReadEmbeddedImagesByIndex(glbBytes);

        using var initCmd = device.CreateGraphicsCommandList($"{namePrefix}.init");
        var stagingBuffers = new List<D3D12Buffer>(layout.UniqueImageIndices.Count + 2);
        var uniqueTextures = new D3D12Texture[layout.UniqueImageIndices.Count];
        D3D12Texture? white = null;
        D3D12Texture? flatNormal = null;
        try
        {
            initCmd.Begin(0);
            for (var slot = 0; slot < layout.UniqueImageIndices.Count; slot++)
            {
                var imageIndex = layout.UniqueImageIndices[slot];
                var decoded = DecodeOrMagenta(images, imageIndex);
                uniqueTextures[slot] = PbrMaterialMaps.CreateMippedTexture(
                    device, initCmd, $"{namePrefix}.material.img{imageIndex}", decoded, stagingBuffers);
            }

            white = PbrMaterialMaps.CreateWhite(device, initCmd, $"{namePrefix}.material.white", stagingBuffers);
            flatNormal = PbrMaterialMaps.CreateFlatNormal(device, initCmd, $"{namePrefix}.material.flatnormal", stagingBuffers);

            initCmd.End();
            initCmd.ExecuteOn(device);
            device.WaitForIdle();
        }
        catch
        {
            DisposeAll(uniqueTextures);
            white?.Dispose();
            flatNormal?.Dispose();
            throw;
        }
        finally
        {
            foreach (var staging in stagingBuffers)
            {
                staging.Dispose();
            }
        }

        return new MultiMaterialAtlas(device, white!, flatNormal!, uniqueTextures, layout.MaterialSlots);
    }

    private static DecodedImage DecodeOrMagenta(IReadOnlyDictionary<int, GltfTextureBlob> images, int gltfImageIndex)
    {
        if (!images.TryGetValue(gltfImageIndex, out var blob))
        {
            return MagentaErrorImage();
        }

        try
        {
            return ImageDecoder.DecodeRgba8(blob.Bytes);
        }
        catch (InvalidDataException)
        {
            return MagentaErrorImage();
        }
    }

    private static DecodedImage MagentaErrorImage() => new(MagentaErrorExtent, MagentaErrorExtent, MagentaErrorRgba);

    private static void DisposeAll(D3D12Texture?[] textures)
    {
        foreach (var texture in textures)
        {
            texture?.Dispose();
        }
    }
}
