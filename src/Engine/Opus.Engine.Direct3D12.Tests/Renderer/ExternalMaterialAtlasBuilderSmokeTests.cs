using System;
using System.IO;
using FluentAssertions;
using Opus.Content.Meshes;
using Opus.Content.Textures;
using Opus.Engine.Direct3D12.Tests.Fixtures;
using Opus.Engine.Renderer.Direct3D12.Scene;
using Opus.Engine.Rhi.Direct3D12;
using Xunit;

namespace Opus.Engine.Direct3D12.Tests.Renderer;

/// <summary>GPU smoke for <see cref="ExternalMaterialAtlasBuilder.BuildFromDirectory"/> — the
/// disk-loaded PBR texture path. Reads the material names from the sample GLB, then resolves +
/// uploads loose <c>{root}/{name}/{name}_{map}.png</c> files under a temp directory. Two cases:
/// a missing directory falls every material back to the neutral run (no images uploaded), and a
/// single seeded base-colour file uploads exactly one image. The base-colour PNG is synthesised
/// through the engine's own spec-compliant <see cref="D3D12ScreenshotPngWriter"/> so the fixture
/// stays self-contained — no checked-in binary, no hand-rolled encoder bytes — while still
/// exercising the real decode + mip + upload path on hardware.</summary>
public sealed class ExternalMaterialAtlasBuilderSmokeTests
{
    private const int SeedTextureExtent = 8;

    [SkippableFact]
    public void Build_from_a_directory_with_no_textures_falls_every_material_back_to_neutral()
    {
        using var host = D3D12SmokeHost.OpenWindow("opus-d3d12-ext-atlas-smoke", width: 320, height: 240);
        using var asset = AlphaSmokeGltfAsset.WriteTempGlb();
        var glb = File.ReadAllBytes(asset.Path);
        var expected = GltfImageReader.ReadMaterialBindings(glb).Count;
        Skip.If(expected == 0, "The smoke GLB declares no materials to build an atlas from.");

        var missingRoot = Path.Combine(Path.GetTempPath(), $"opus-ext-atlas-missing-{Guid.NewGuid():N}");
        using var atlas = ExternalMaterialAtlasBuilder.BuildFromDirectory(host.Session.Device, glb, missingRoot, "ext-smoke-a");

        atlas.UniqueImageCount.Should().Be(0, "no loose files exist under the missing root");
        atlas.MaterialCount.Should().Be(expected);
    }

    [SkippableFact]
    public void Build_from_a_directory_with_one_basecolor_file_uploads_a_single_image()
    {
        using var host = D3D12SmokeHost.OpenWindow("opus-d3d12-ext-atlas-smoke", width: 320, height: 240);
        using var asset = AlphaSmokeGltfAsset.WriteTempGlb();
        var glb = File.ReadAllBytes(asset.Path);
        var bindings = GltfImageReader.ReadMaterialBindings(glb);
        Skip.If(bindings.Count == 0, "The smoke GLB declares no material to attach a loose texture to.");

        var materialName = bindings[0].Name;
        var root = Path.Combine(Path.GetTempPath(), $"opus-ext-atlas-{Guid.NewGuid():N}");
        try
        {
            // ImageDecoder picks the codec by content, so the .png convention is cosmetic; the
            // writer creates the {root}/{name}/ directory chain as it lays down the file.
            WriteDecodablePng(ExternalMaterialAtlasPlan.MapPath(root, materialName, ExternalMaterialAtlasPlan.BaseColorMap));

            using var atlas = ExternalMaterialAtlasBuilder.BuildFromDirectory(host.Session.Device, glb, root, "ext-smoke-b");

            atlas.UniqueImageCount.Should().Be(1, "only the base-colour map was seeded on disk");
            atlas.MaterialCount.Should().Be(bindings.Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [SkippableFact]
    public void Build_from_a_full_map_set_block_compresses_and_caches_every_map()
    {
        using var host = D3D12SmokeHost.OpenWindow("opus-d3d12-ext-atlas-bc-smoke", width: 320, height: 240);
        using var asset = AlphaSmokeGltfAsset.WriteTempGlb();
        var glb = File.ReadAllBytes(asset.Path);
        var bindings = GltfImageReader.ReadMaterialBindings(glb);
        Skip.If(bindings.Count == 0, "The smoke GLB declares no material to attach a loose texture set to.");

        var materialName = bindings[0].Name;
        var root = Path.Combine(Path.GetTempPath(), $"opus-ext-atlas-bc-{Guid.NewGuid():N}");
        try
        {
            foreach (var map in new[]
            {
                ExternalMaterialAtlasPlan.BaseColorMap,
                ExternalMaterialAtlasPlan.NormalMap,
                ExternalMaterialAtlasPlan.OrmMap,
                ExternalMaterialAtlasPlan.EmissiveMap,
            })
            {
                WriteDecodablePng(ExternalMaterialAtlasPlan.MapPath(root, materialName, map));
            }

            using var atlas = ExternalMaterialAtlasBuilder.BuildFromDirectory(host.Session.Device, glb, root, "ext-smoke-bc");

            // Base colour + ORM + emissive upload as BC7, the normal as BC5 — all four through the
            // real encode → cache → block-aware upload path on the GPU.
            atlas.UniqueImageCount.Should().Be(4, "base colour, normal, ORM and emissive were all seeded");
            atlas.MaterialCount.Should().Be(bindings.Count);
            File.Exists(
                ExternalMaterialAtlasPlan.MapPath(root, materialName, ExternalMaterialAtlasPlan.NormalMap)
                + CompressedTextureCache.CacheFileExtension)
                .Should().BeTrue("the BC5 normal encode is cooked to a sibling cache file");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void WriteDecodablePng(string path)
    {
        var rgba = new byte[SeedTextureExtent * SeedTextureExtent * 4];
        Array.Fill(rgba, byte.MaxValue);
        var screenshot = new D3D12Screenshot(SeedTextureExtent, SeedTextureExtent, rgba, "rgba8", SeedTextureExtent * 4);
        D3D12ScreenshotPngWriter.Write(path, screenshot, D3D12ScreenshotMetadata.Empty);
    }
}
