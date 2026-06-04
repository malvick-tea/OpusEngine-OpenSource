using System;
using System.Collections.Generic;
using System.IO;
using Opus.Content.Meshes;
using Opus.Content.Textures;
using Opus.Engine.Rhi.Direct3D12;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>Assembles a <see cref="MultiMaterialAtlas"/> from loose image files on disk — the
/// runtime path for large 4K PBR sets that are too big to embed in the glb (see
/// <c>content/maps/japan/TEXTURE_SPEC.md</c>). Reads the material names + scalar factors from the
/// scene glb (ignoring any embedded images), resolves each material's maps under
/// <paramref name="texturesRoot"/> via <see cref="ExternalMaterialAtlasPlan"/>, then loads + uploads
/// the files that exist. Materials whose maps are absent (textures not authored yet) route through
/// the same neutral fallbacks as the embedded builder, so a partially-textured map still renders.
/// Shares the upload + heap + run plumbing with <see cref="MultiMaterialAtlasBuilder"/> through
/// <see cref="PbrMaterialMaps"/> + the shared <see cref="MultiMaterialAtlas"/>.</summary>
public static class ExternalMaterialAtlasBuilder
{
    public static MultiMaterialAtlas BuildFromDirectory(
        D3D12RhiDevice device,
        ReadOnlySpan<byte> sceneGlbBytes,
        string texturesRoot,
        string namePrefix)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(texturesRoot);

        var bindings = GltfImageReader.ReadMaterialBindings(sceneGlbBytes);
        var layout = ExternalMaterialAtlasPlan.Build(bindings, texturesRoot, File.Exists);

        using var initCmd = device.CreateGraphicsCommandList($"{namePrefix}.init");
        var stagingBuffers = new List<D3D12Buffer>(layout.UniqueImages.Count + 2);
        var uniqueTextures = new D3D12Texture[layout.UniqueImages.Count];
        D3D12Texture? white = null;
        D3D12Texture? flatNormal = null;
        try
        {
            initCmd.Begin(0);
            for (var slot = 0; slot < layout.UniqueImages.Count; slot++)
            {
                uniqueTextures[slot] = LoadCompressedImage(
                    device, initCmd, $"{namePrefix}.ext.img{slot}", layout.UniqueImages[slot], stagingBuffers);
            }

            white = PbrMaterialMaps.CreateWhite(device, initCmd, $"{namePrefix}.ext.white", stagingBuffers);
            flatNormal = PbrMaterialMaps.CreateFlatNormal(device, initCmd, $"{namePrefix}.ext.flatnormal", stagingBuffers);

            initCmd.End();
            initCmd.ExecuteOn(device);
            device.WaitForIdle();
        }
        catch
        {
            foreach (var texture in uniqueTextures)
            {
                texture?.Dispose();
            }

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

    /// <summary>Loads one loose image, block-compresses it to the format its map kind dictates
    /// (cached on disk so the slow encode runs once), and uploads the BC mip chain. Base colour +
    /// emissive → BC7 sRGB, the packed ORM → BC7 linear, normals → BC5.</summary>
    private static D3D12Texture LoadCompressedImage(
        D3D12RhiDevice device,
        D3D12CommandList cmd,
        string name,
        ExternalMaterialImage image,
        List<D3D12Buffer> staging)
    {
        var sourceBytes = File.ReadAllBytes(image.Path);
        var decoded = ImageDecoder.DecodeRgba8(sourceBytes);
        var (encoderFormat, gpuFormat) = ExternalTextureCompression.For(image.Kind);
        var compressed = CompressedTextureCache.GetOrCreate(
            image.Path, sourceBytes, () => BcnTextureEncoder.EncodeMipChain(decoded, encoderFormat));
        return PbrMaterialMaps.CreateCompressedTexture(device, cmd, name, compressed, gpuFormat, staging);
    }
}
