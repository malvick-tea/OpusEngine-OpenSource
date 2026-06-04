using System;
using System.Collections.Generic;
using System.Numerics;
using Opus.Content.Meshes;
using Opus.Content.Textures;
using Opus.Engine.Renderer.Direct3D12.Assets;
using Opus.Engine.Rhi;
using Opus.Engine.Rhi.Direct3D12;
using Opus.Foundation.Geometry;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>Owns every GPU-resident artefact the Garage scene needs to render one tank
/// model: the parsed glTF primitives, the procedural floor + projectile primitives
/// stitched into the same flat <see cref="GpuScene"/>, the optional shell-mesh glTF
/// spliced in alongside (per <c>data/shell-visuals.csv</c>), the single-texture albedo
/// atlas, the flattened tank-template draw list, and the static-fixture draw list
/// (floor). Lifecycle is one-shot — construct via <see cref="Load"/>, consume, dispose.</summary>
/// <remarks>
/// Extracted from <see cref="GarageSceneController"/> in M4.t so the controller stops
/// being responsible for asset loading, atlas building, and GpuScene augmentation at the
/// same time it tracks orbit / follow / pose state. Each side now changes for one reason.
/// Shell-mesh splice landed in Phase 7 — the shell is loaded through the same
/// <see cref="D3D12GltfSceneLoader"/> path as the tank and remapped into the merged
/// slice table so <see cref="SceneNodeDrawTransformer.Instantiate"/> can fan it out per
/// in-flight AP projectile.
/// </remarks>
public sealed class GarageSceneAssets : IDisposable
{
    /// <summary>Olive multiplier applied to the floor primitive's albedo. Reads as
    /// "earth / parade ground" without needing a second texture in the atlas.</summary>
    private static readonly Vector4 FloorTintFactor = new(0.35f, 0.38f, 0.32f, 1f);

    private readonly GltfSceneGpuAssets _glbScene;
    private readonly GltfSceneGpuAssets? _shellScene;
    private readonly GpuPrimitive _floorPrimitive;
    private readonly GpuPrimitive _projectilePrimitive;
    private readonly GpuPrimitive _casingPrimitive;
    private bool _disposed;

    private GarageSceneAssets(
        GltfSceneGpuAssets glbScene,
        GltfSceneGpuAssets? shellScene,
        GpuPrimitive floorPrimitive,
        GpuPrimitive projectilePrimitive,
        GpuPrimitive casingPrimitive,
        GpuScene augmentedGpuScene,
        IMaterialAtlas atlas,
        SceneNodeDraw[] staticDraws,
        int projectileMeshIndex,
        int casingMeshIndex,
        IReadOnlyList<SceneNodeDraw>? shellTemplate,
        Aabb bounds,
        IReadOnlyList<Aabb> meshLocalBounds)
    {
        _glbScene = glbScene;
        _shellScene = shellScene;
        _floorPrimitive = floorPrimitive;
        _projectilePrimitive = projectilePrimitive;
        _casingPrimitive = casingPrimitive;
        GpuScene = augmentedGpuScene;
        Atlas = atlas;
        StaticDraws = staticDraws;
        ProjectileMeshIndex = projectileMeshIndex;
        CasingMeshIndex = casingMeshIndex;
        ShellTemplate = shellTemplate;
        Bounds = bounds;
        MeshLocalBounds = meshLocalBounds;
    }

    public GpuScene GpuScene { get; }

    public IMaterialAtlas Atlas { get; }

    public IReadOnlyList<SceneNodeDraw> TankTemplate => _glbScene.NodeDraws;

    /// <summary>Parsed tank hierarchy retained for consumers that pose named nodes at runtime.</summary>
    public GltfScene TankScene => _glbScene.Scene;

    public IReadOnlyList<SceneNodeDraw> StaticDraws { get; }

    public int ProjectileMeshIndex { get; }

    /// <summary>Slice index of a small procedural box mesh usable by callers that need a
    /// neutral placeholder prop without loading another asset.</summary>
    public int PropBoxMeshIndex => ProjectileMeshIndex;

    /// <summary>Slice index of the procedural casing cylinder in the merged scene. Demo
    /// hosts feed one world matrix per live <c>CasingVisual</c> into
    /// <see cref="GarageSceneController.CasingProjectiles"/> to render ejected casings.</summary>
    public int CasingMeshIndex { get; }

    /// <summary>Flattened node-draw list for the shell glTF, mesh indices already remapped
    /// to the merged <see cref="GpuScene"/>. Null when no shell asset was loaded (the
    /// renderer falls back to the procedural projectile cube for every round). When
    /// present, callers fan it across per-projectile world matrices via
    /// <see cref="SceneNodeDrawTransformer.Instantiate"/>, exactly as the tank template
    /// is fanned across the player + opponents.</summary>
    public IReadOnlyList<SceneNodeDraw>? ShellTemplate { get; }

    public Aabb Bounds { get; }

    /// <summary>Per-mesh local-space AABB for the merged <see cref="GpuScene"/>, indexed by
    /// mesh to match <see cref="Assets.GpuScene.SlicesByMesh"/>. Tank and shell meshes carry
    /// their real glTF bounds; the procedural floor, projectile, and casing slices are tagged
    /// <see cref="Aabb.Empty"/> so frustum culling always keeps them (single-draw ground +
    /// transients, whose CPU geometry is not retained at this layer). Feed straight into
    /// <see cref="D3D12ForwardSceneRenderer.Render(D3D12Renderer, GpuScene, IReadOnlyList{SceneNodeDraw}, IMaterialAtlas, FrameCameraSet, LightingSetup, PostFxSetup, IReadOnlyList{Aabb})"/>
    /// to cull off-screen tank/shell instances on a wide map.</summary>
    public IReadOnlyList<Aabb> MeshLocalBounds { get; }

    /// <summary>End-to-end load: parse + upload the tank GLB, build a single-texture atlas
    /// over the first embedded base-colour image (white fallback if absent), upload the
    /// procedural floor + projectile primitives, optionally load + splice a shell glTF
    /// from <paramref name="shellAssetPath"/> when supplied, merge everything into one
    /// augmented <see cref="GpuScene"/>, and build the static draw list pinning the floor
    /// at the origin.</summary>
    public static GarageSceneAssets Load(
        D3D12RhiDevice device,
        string glbPath,
        string namePrefix,
        string? shellAssetPath = null)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentException.ThrowIfNullOrWhiteSpace(glbPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(namePrefix);

        var glbScene = D3D12GltfSceneLoader.Load(device, glbPath, namePrefix);
        var atlas = BuildAtlas(device, glbScene, namePrefix);
        var floorPrimitive = FloorPrimitiveUploader.Upload(device, namePrefix);
        var projectilePrimitive = ProjectilePrimitiveUploader.Upload(device, namePrefix);
        var casingPrimitive = CasingPrimitiveUploader.Upload(device, namePrefix);
        var shellScene = shellAssetPath is null
            ? null
            : D3D12GltfSceneLoader.Load(device, shellAssetPath, $"{namePrefix}.shell");

        var splice = SpliceProceduralMeshes(
            glbScene.GpuScene, glbScene.MeshLocalBounds, floorPrimitive, projectilePrimitive,
            casingPrimitive, shellScene);
        var staticDraws = new[] { new SceneNodeDraw(splice.FloorMeshIndex, Matrix4x4.Identity) };
        var bounds = shellScene is null ? glbScene.Bounds : glbScene.Bounds.Union(shellScene.Bounds);

        return new GarageSceneAssets(
            glbScene, shellScene, floorPrimitive, projectilePrimitive, casingPrimitive,
            splice.Augmented, atlas, staticDraws, splice.ProjectileMeshIndex, splice.CasingMeshIndex,
            splice.ShellTemplate, bounds, splice.MeshLocalBounds);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        DisposePrimitives(_glbScene.GpuScene.Primitives);
        if (_shellScene is not null)
        {
            DisposePrimitives(_shellScene.GpuScene.Primitives);
        }

        _floorPrimitive.Vb.Dispose();
        _floorPrimitive.Ib.Dispose();
        _projectilePrimitive.Vb.Dispose();
        _projectilePrimitive.Ib.Dispose();
        _casingPrimitive.Vb.Dispose();
        _casingPrimitive.Ib.Dispose();
        Atlas.Dispose();
        _disposed = true;
    }

    private static void DisposePrimitives(GpuPrimitive[] primitives)
    {
        foreach (var prim in primitives)
        {
            prim.Vb.Dispose();
            prim.Ib.Dispose();
        }
    }

    private readonly record struct SpliceResult(
        GpuScene Augmented,
        int FloorMeshIndex,
        int ProjectileMeshIndex,
        int CasingMeshIndex,
        IReadOnlyList<SceneNodeDraw>? ShellTemplate,
        IReadOnlyList<Aabb> MeshLocalBounds);

    /// <summary>Builds the merged <see cref="GpuScene"/> that backs the whole frame.
    /// Layout in primitives + slices: tank-GLB primitives, then floor, projectile cube,
    /// casing cylinder, then (optional) shell glTF primitives. Slice indices are stable
    /// so callers can hand <see cref="FloorMeshIndex"/> / <see cref="ProjectileMeshIndex"/>
    /// / <see cref="CasingMeshIndex"/> directly to <see cref="SceneNodeDraw"/>.</summary>
    private static SpliceResult SpliceProceduralMeshes(
        GpuScene source,
        IReadOnlyList<Aabb> tankBounds,
        GpuPrimitive floor,
        GpuPrimitive projectile,
        GpuPrimitive casing,
        GltfSceneGpuAssets? shellScene)
    {
        const int ProceduralPrimitiveCount = 3;
        var shellPrimCount = shellScene?.GpuScene.Primitives.Length ?? 0;
        var shellSliceCount = shellScene?.GpuScene.SlicesByMesh.Length ?? 0;
        var tankPrimCount = source.Primitives.Length;
        var tankSliceCount = source.SlicesByMesh.Length;

        var primitives = new GpuPrimitive[tankPrimCount + ProceduralPrimitiveCount + shellPrimCount];
        Array.Copy(source.Primitives, primitives, tankPrimCount);
        primitives[tankPrimCount] = floor;
        primitives[tankPrimCount + 1] = projectile;
        primitives[tankPrimCount + 2] = casing;
        if (shellScene is not null)
        {
            Array.Copy(shellScene.GpuScene.Primitives, 0, primitives,
                tankPrimCount + ProceduralPrimitiveCount, shellPrimCount);
        }

        var slices = new GpuMeshSlice[tankSliceCount + ProceduralPrimitiveCount + shellSliceCount];
        Array.Copy(source.SlicesByMesh, slices, tankSliceCount);
        var floorMeshIndex = tankSliceCount;
        var projectileMeshIndex = tankSliceCount + 1;
        var casingMeshIndex = tankSliceCount + 2;
        slices[floorMeshIndex] = new GpuMeshSlice(tankPrimCount, 1);
        slices[projectileMeshIndex] = new GpuMeshSlice(tankPrimCount + 1, 1);
        slices[casingMeshIndex] = new GpuMeshSlice(tankPrimCount + 2, 1);

        var shellTemplate = shellScene is null
            ? null
            : BuildRemappedShellTemplate(
                shellScene,
                tankPrimCount + ProceduralPrimitiveCount,
                tankSliceCount + ProceduralPrimitiveCount,
                slices);

        var meshLocalBounds = BuildMergedMeshBounds(
            tankBounds, tankSliceCount, ProceduralPrimitiveCount, shellSliceCount,
            floorMeshIndex, projectileMeshIndex, casingMeshIndex, slices.Length, shellScene);

        return new SpliceResult(
            new GpuScene(primitives, slices),
            floorMeshIndex, projectileMeshIndex, casingMeshIndex, shellTemplate, meshLocalBounds);
    }

    /// <summary>Builds the per-mesh local-AABB table for the merged scene in the same mesh
    /// order as the merged slice table: tank meshes carry their real glTF bounds, the three
    /// procedural slices (floor, projectile, casing) are <see cref="Aabb.Empty"/> so culling
    /// always keeps them, and shell meshes carry the shell glTF's bounds. Defensive against a
    /// short <paramref name="tankBounds"/> (treats a missing entry as Empty / always-kept)
    /// so a malformed asset never makes culling drop a node it cannot bound.</summary>
    private static Aabb[] BuildMergedMeshBounds(
        IReadOnlyList<Aabb> tankBounds,
        int tankSliceCount,
        int proceduralPrimitiveCount,
        int shellSliceCount,
        int floorMeshIndex,
        int projectileMeshIndex,
        int casingMeshIndex,
        int totalSlices,
        GltfSceneGpuAssets? shellScene)
    {
        var meshBounds = new Aabb[totalSlices];
        for (var i = 0; i < tankSliceCount; i++)
        {
            meshBounds[i] = i < tankBounds.Count ? tankBounds[i] : Aabb.Empty;
        }

        meshBounds[floorMeshIndex] = Aabb.Empty;
        meshBounds[projectileMeshIndex] = Aabb.Empty;
        meshBounds[casingMeshIndex] = Aabb.Empty;

        if (shellScene is not null)
        {
            var shellBounds = shellScene.MeshLocalBounds;
            var shellOffset = tankSliceCount + proceduralPrimitiveCount;
            for (var s = 0; s < shellSliceCount; s++)
            {
                meshBounds[shellOffset + s] = s < shellBounds.Count ? shellBounds[s] : Aabb.Empty;
            }
        }

        return meshBounds;
    }

    /// <summary>Copies the shell's per-mesh slices into the merged slice table at
    /// <paramref name="sliceOffset"/> (shifting each slice's <c>Start</c> by
    /// <paramref name="primOffset"/>), and returns a remapped copy of the shell's
    /// node-draw list with every <c>MeshIndex</c> rebased into the merged slice space.
    /// Result is ready to feed into <see cref="SceneNodeDrawTransformer.Instantiate"/>.</summary>
    private static IReadOnlyList<SceneNodeDraw> BuildRemappedShellTemplate(
        GltfSceneGpuAssets shellScene,
        int primOffset,
        int sliceOffset,
        GpuMeshSlice[] slices)
    {
        for (var s = 0; s < shellScene.GpuScene.SlicesByMesh.Length; s++)
        {
            var original = shellScene.GpuScene.SlicesByMesh[s];
            slices[sliceOffset + s] = new GpuMeshSlice(primOffset + original.Start, original.Count);
        }

        var remapped = new SceneNodeDraw[shellScene.NodeDraws.Count];
        for (var d = 0; d < shellScene.NodeDraws.Count; d++)
        {
            var orig = shellScene.NodeDraws[d];
            remapped[d] = orig with { MeshIndex = sliceOffset + orig.MeshIndex };
        }

        return remapped;
    }

    private static IMaterialAtlas BuildAtlas(D3D12RhiDevice device, GltfSceneGpuAssets scene, string namePrefix)
    {
        var blob = GltfImageReader.TryReadFirstBaseColorImage(scene.GlbBytes)
            ?? GltfImageReader.TryReadFirstEmbeddedImage(scene.GlbBytes);
        var decoded = blob is null
            ? new DecodedImage(1, 1, new byte[] { 255, 255, 255, 255 })
            : ImageDecoder.DecodeRgba8(blob.Bytes);
        return UploadAtlas(device, decoded, namePrefix);
    }

    private static IMaterialAtlas UploadAtlas(D3D12RhiDevice device, DecodedImage decoded, string namePrefix)
    {
        var albedo = device.CreateGraphicsTexture(new RhiTextureDescription(
            $"{namePrefix}.albedo", decoded.Width, decoded.Height, 1,
            RhiTextureFormat.Rgba8Unorm, RhiTextureUsage.Sampled));
        using (var initCmd = device.CreateGraphicsCommandList($"{namePrefix}.albedo.init"))
        {
            initCmd.Begin(0);
            using var staging = device.ScheduleTextureUpload(albedo, decoded.Rgba, initCmd);
            initCmd.End();
            initCmd.ExecuteOn(device);
            device.WaitForIdle();
        }

        return new SingleTextureAtlas(device, albedo, Vector4.One, FloorTintFactor);
    }
}
