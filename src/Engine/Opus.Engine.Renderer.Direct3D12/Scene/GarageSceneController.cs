using System;
using System.Collections.Generic;
using System.Numerics;
using Opus.Content;
using Opus.Engine.Renderer;
using Opus.Engine.Renderer.Direct3D12.Assets;
using Opus.Engine.Renderer.Scene;
using Opus.Engine.Rhi;
using Opus.Engine.Rhi.Direct3D12;
using Silk.NET.DXGI;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>Top-level orchestrator for the Garage scene. Composes a
/// <see cref="D3D12ForwardSceneRenderer"/>, a <see cref="GarageSceneAssets"/> asset
/// bundle, a <see cref="GarageCameraController"/>, an <see cref="OrbitControlState"/>,
/// and the per-frame pose inputs (player tank + opponents + projectiles + shell heads).
/// Walks the frame: orbit phase advance → follow lerp → build draw list via
/// <see cref="GarageSceneDrawBuilder"/> → forward render.
/// </summary>
/// <remarks>
/// Lifecycle: construct → <see cref="LoadAsset"/> → repeatedly <see cref="Tick"/> +
/// <see cref="Render"/> → <see cref="Dispose"/>. Asset loading lives in
/// <see cref="GarageSceneAssets"/>; camera-state arithmetic lives in
/// <see cref="GarageCameraController"/>; per-frame draw-list permutation lives in
/// <see cref="GarageSceneDrawBuilder"/>. This class owns only orchestration + pose
/// inputs, so it changes when the render composition does — not when camera feel, asset
/// format, or draw permutation changes.
/// </remarks>
public sealed class GarageSceneController : IDisposable
{
    private const float DefaultSpinRadiansPerSecond = 0.35f;

    private readonly D3D12RhiDevice _device;
    private readonly D3D12ForwardSceneRenderer _sceneRenderer;
    private readonly LightingSetup _lighting;
    private readonly PostFxSetup _postFx;
    private readonly OrbitControlState _orbit;
    private readonly int _viewportWidth;
    private readonly int _viewportHeight;

    private GarageSceneAssets? _assets;
    private GarageCameraController? _cameraCtl;
    private bool _disposed;

    /// <summary>Construct the controller with a canon-tuned <paramref name="lightingPreset"/>
    /// loaded from <c>data/*-lighting.csv</c>. There is no built-in default — the preset
    /// is a required dependency so canon lighting lives in data, not C# constants.</summary>
    public GarageSceneController(
        D3D12RhiDevice device,
        D3D12ShaderCompiler compiler,
        Format backbufferFormat,
        int viewportWidth,
        int viewportHeight,
        LightingPreset lightingPreset,
        string namePrefix = "garage")
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(compiler);

        _device = device;
        _viewportWidth = viewportWidth;
        _viewportHeight = viewportHeight;
        _sceneRenderer = new D3D12ForwardSceneRenderer(device, compiler, backbufferFormat, viewportWidth, viewportHeight, namePrefix);
        _orbit = new OrbitControlState(DefaultSpinRadiansPerSecond);
        _lighting = new LightingSetup(
            new DirectionalLight(lightingPreset.SunDirection, lightingPreset.SunColour, Intensity: 1f, CastsShadows: true),
            Array.Empty<LocalLight>(),
            new SkySetup(lightingPreset.SunDirection, lightingPreset.AmbientColour, lightingPreset.HorizonColour, ExposureEv: 0f, EnvironmentMapHandle: 0));
        _postFx = new PostFxSetup(
            TonemapOperator.AcesFilmic,
            new BloomSetup(Enabled: false, Threshold: 1f, Intensity: 0f, MipChainLevels: 0),
            new ColourGradingSetup(Enabled: false, LutHandle: 0, Saturation: 1f, Contrast: 1f),
            AntiAliasingMode.None,
            UpscaleMode.None,
            ExposureEv: 0f);
    }

    public bool IsLoaded => _assets is not null;

    public int LastDrawnPrimitiveCount => _sceneRenderer.LastDrawnPrimitiveCount;

    public OrbitControlState Orbit => _orbit;

    public Matrix4x4 TankWorld { get; set; } = Matrix4x4.Identity;

    /// <summary>Per-instance albedo tint for the player tank. Multiplies the resolved
    /// material factor in the forward pass. <c>Vector4.One</c> = no tint (default).</summary>
    public Vector4 TankTint { get; set; } = Vector4.One;

    public IReadOnlyList<Matrix4x4>? OpponentTanks { get; set; }

    /// <summary>Optional parallel list of per-opponent albedo tints — same length as
    /// <see cref="OpponentTanks"/>. When null, every opponent renders at identity tint.
    /// When set, each opponent multiplies its rendered material by the matching entry.
    /// Enables per-instance camo (e.g. grey, winter white) without
    /// uploading separate albedo textures.</summary>
    public IReadOnlyList<Vector4>? OpponentTints { get; set; }

    /// <summary>Trail-echo cubes for in-flight projectiles. Each entry is one world matrix
    /// for the procedural projectile cube mesh; <see cref="SimToWorldMapper.BuildProjectileTrail"/>
    /// produces N echoes per round so the trail reads as motion.</summary>
    public IReadOnlyList<Matrix4x4>? Projectiles { get; set; }

    /// <summary>One world matrix per in-flight AP projectile, oriented along the velocity
    /// vector. Renders the bundled shell glTF (loaded via <see cref="GarageSceneAssets"/>'s
    /// optional shell-asset path) at the head position. Null/empty falls back to the
    /// trail-cube path. Has no effect when no shell template was loaded.</summary>
    public IReadOnlyList<Matrix4x4>? ShellProjectiles { get; set; }

    /// <summary>One world matrix per live ejected casing produced by the demo-side
    /// <c>CasingEjector</c>. Each entry renders one full <see cref="CasingMesh"/> cylinder
    /// — typically tumbling away from a tank that fired the previous tick. Null/empty
    /// skips the casing draw section entirely.</summary>
    public IReadOnlyList<Matrix4x4>? CasingProjectiles { get; set; }

    public void SetTankPose(Vector3 position, float yawRadians) =>
        TankWorld = BuildTankWorld(position, yawRadians);

    public static Matrix4x4 BuildTankWorld(Vector3 position, float yawRadians) =>
        Matrix4x4.CreateRotationY(yawRadians) * Matrix4x4.CreateTranslation(position);

    /// <summary>Load the tank-and-floor asset bundle. Existing entry point used by tests
    /// + the legacy garage demo — backwards compatible with the M3-wrap.c signature.</summary>
    public void LoadAsset(string glbPath) => LoadAsset(glbPath, shellAssetPath: null);

    /// <summary>Load the tank-and-floor asset bundle plus an optional shell glTF.
    /// <paramref name="shellAssetPath"/> may be null to skip shell loading (the renderer
    /// falls back to the procedural projectile cube path). When supplied, the shell mesh
    /// is spliced into the same <see cref="GpuScene"/> and exposed via
    /// <see cref="GarageSceneAssets.ShellTemplate"/>.</summary>
    public void LoadAsset(string glbPath, string? shellAssetPath)
    {
        ThrowIfDisposed();
        if (_assets is not null)
        {
            throw new InvalidOperationException("Garage controller already has an asset loaded.");
        }

        _assets = GarageSceneAssets.Load(_device, glbPath, "garage", shellAssetPath);
        _cameraCtl = new GarageCameraController(_assets.Bounds, _viewportWidth, _viewportHeight);
    }

    public void Zoom(float ticks)
    {
        ThrowIfDisposed();
        _cameraCtl?.Zoom(ticks);
    }

    public void PitchCamera(float deltaPixels)
    {
        ThrowIfDisposed();
        _cameraCtl?.Pitch(deltaPixels);
    }

    public void Tick(float deltaSeconds)
    {
        ThrowIfDisposed();
        _orbit.Tick(deltaSeconds);
        _cameraCtl?.Advance(deltaSeconds, TankWorld.Translation);
    }

    public void Render(D3D12Renderer renderer)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(renderer);
        if (_assets is null || _cameraCtl is null)
        {
            throw new InvalidOperationException("Garage controller has no asset loaded — call LoadAsset first.");
        }

        var followCamera = _cameraCtl.Current;
        var (cameraPos, view, _) = followCamera.At(_orbit.Phase);
        var cameras = OrbitFrameCameraBuilder.Build(in followCamera, in view, in cameraPos);
        var tanks = new GarageSceneDrawBuilder.TankInstancesInput(
            TankWorld, TankTint, OpponentTanks, OpponentTints);
        var transients = new GarageSceneDrawBuilder.TransientsInput(
            Projectiles, _assets.ProjectileMeshIndex,
            _assets.ShellTemplate, ShellProjectiles,
            CasingProjectiles, _assets.CasingMeshIndex);
        var draws = GarageSceneDrawBuilder.Build(_assets.TankTemplate, _assets.StaticDraws, in tanks, in transients);
        _sceneRenderer.Render(
            renderer, _assets.GpuScene, draws, _assets.Atlas, cameras, _lighting, _postFx,
            _assets.MeshLocalBounds);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _assets?.Dispose();
        _sceneRenderer.Dispose();
        _disposed = true;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
