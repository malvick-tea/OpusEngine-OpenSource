using System;
using System.Collections.Generic;
using System.Numerics;
using Opus.Engine.Renderer;

namespace Opus.Engine.Renderer.Direct3D12.Alpha;

public readonly record struct D3D12AlphaRect(int X, int Y, int Width, int Height);

/// <summary>Canonical Opus 0.1 alpha-frame contract for the D3D12 path: one offscreen
/// scene viewport composited through the D3D12 UI pass, with a wide-map camera and enough
/// repeated actors/transients to catch black-window, empty-scene, and descriptor-binding
/// regressions in a single smoke.</summary>
public sealed record D3D12AlphaFramePlan(
    D3D12AlphaRect SceneViewport,
    FrameCameraSet Cameras,
    LightingSetup Lighting,
    PostFxSetup PostFx,
    IReadOnlyList<Matrix4x4> OpponentTanks,
    IReadOnlyList<Vector4> OpponentTints,
    IReadOnlyList<Matrix4x4> ProjectileTrails,
    IReadOnlyList<Matrix4x4> Casings,
    string[] UiText)
{
    public const int DefaultOpponentColumns = 10;
    public const int DefaultOpponentRows = 8;
    public const int DefaultProjectileTrails = 12;
    public const int DefaultCasings = 16;

    /// <summary>Smallest back-buffer the alpha frame plan accepts. The host clamps resize
    /// requests below this (e.g. a minimised window reporting a tiny client area) to a
    /// no-op so the plan never has to lay out a degenerate viewport.</summary>
    public const int MinimumBackBufferWidth = 160;

    /// <summary>Smallest back-buffer height the alpha frame plan accepts. See
    /// <see cref="MinimumBackBufferWidth"/>.</summary>
    public const int MinimumBackBufferHeight = 120;

    public static readonly TimeSpan AlphaFrameBudget = TimeSpan.FromMilliseconds(33.4);

    public int MapInstanceCount =>
        1 + OpponentTanks.Count + ProjectileTrails.Count + Casings.Count;

    public static D3D12AlphaFramePlan Create(
        int backBufferWidth,
        int backBufferHeight,
        int opponentColumns = DefaultOpponentColumns,
        int opponentRows = DefaultOpponentRows,
        int projectileTrails = DefaultProjectileTrails,
        int casings = DefaultCasings)
    {
        if (backBufferWidth < MinimumBackBufferWidth || backBufferHeight < MinimumBackBufferHeight)
        {
            throw new ArgumentOutOfRangeException(
                nameof(backBufferWidth),
                $"Alpha D3D12 smoke needs at least a {MinimumBackBufferWidth}x{MinimumBackBufferHeight} back buffer.");
        }

        if (opponentColumns <= 0 || opponentRows <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(opponentColumns), "Grid dimensions must be positive.");
        }

        if (projectileTrails < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(projectileTrails), "ProjectileTrails must be non-negative.");
        }

        if (casings < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(casings), "Casings must be non-negative.");
        }

        var sceneViewport = new D3D12AlphaRect(
            X: 12,
            Y: 36,
            Width: Math.Max(64, backBufferWidth - 24),
            Height: Math.Max(64, backBufferHeight - 50));
        var aspect = sceneViewport.Width / (float)sceneViewport.Height;

        var cameraPos = new Vector3(82f, 54f, 96f);
        var cameraTarget = new Vector3(0f, 0f, 0f);
        var view = Matrix4x4.CreateLookAt(cameraPos, cameraTarget, Vector3.UnitY);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(
            MathF.PI / 3.15f,
            aspect,
            nearPlaneDistance: 0.1f,
            farPlaneDistance: 750f);
        var cameras = FrameCameraSet.SingleMain(new CameraSetup(
            view,
            projection,
            cameraPos,
            Vector3.Normalize(cameraTarget - cameraPos),
            NearPlane: 0.1f,
            FarPlane: 750f,
            FovYRadians: MathF.PI / 3.15f,
            AspectRatio: aspect));

        var sunDirection = Vector3.Normalize(new Vector3(-0.35f, -1f, -0.25f));
        var lighting = new LightingSetup(
            new DirectionalLight(sunDirection, new Vector3(1f, 0.96f, 0.88f), Intensity: 1.2f, CastsShadows: false),
            Array.Empty<LocalLight>(),
            new SkySetup(sunDirection, new Vector3(0.42f, 0.52f, 0.64f), new Vector3(0.12f, 0.18f, 0.24f), 0f, 0));
        var postFx = new PostFxSetup(
            TonemapOperator.AcesFilmic,
            new BloomSetup(Enabled: false, Threshold: 1f, Intensity: 0f, MipChainLevels: 0),
            new ColourGradingSetup(Enabled: false, LutHandle: 0, Saturation: 1f, Contrast: 1f),
            AntiAliasingMode.None,
            UpscaleMode.None,
            ExposureEv: 0f);

        var opponents = BuildOpponentGrid(opponentColumns, opponentRows);
        var tints = BuildOpponentTints(opponents.Count);
        var projectileTrailMatrices = BuildTransientLine(projectileTrails, z: -22f, y: 3.5f, spacing: 4.5f);
        var casingMatrices = BuildTransientLine(casings, z: 24f, y: 1.5f, spacing: 3.2f);
        var text = new[]
        {
            "OPUS 0.1 D3D12 ALPHA",
            "Scene viewport + UI + text + model + repeated-map smoke",
        };

        return new D3D12AlphaFramePlan(
            sceneViewport,
            cameras,
            lighting,
            postFx,
            opponents,
            tints,
            projectileTrailMatrices,
            casingMatrices,
            text);
    }

    private static IReadOnlyList<Matrix4x4> BuildOpponentGrid(int columns, int rows)
    {
        var result = new List<Matrix4x4>(columns * rows);
        var xOrigin = -(columns - 1) * 7.5f;
        var zOrigin = -(rows - 1) * 7.5f;

        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < columns; col++)
            {
                var x = xOrigin + (col * 15f);
                var z = zOrigin + (row * 15f);
                var yaw = ((row + col) % 4) * MathF.PI * 0.25f;
                result.Add(
                    Matrix4x4.CreateScale(4.5f)
                    * Matrix4x4.CreateRotationY(yaw)
                    * Matrix4x4.CreateTranslation(x, 0f, z));
            }
        }

        return result;
    }

    private static IReadOnlyList<Vector4> BuildOpponentTints(int count)
    {
        var palette = new[]
        {
            new Vector4(0.86f, 0.88f, 0.80f, 1f),
            new Vector4(0.55f, 0.60f, 0.52f, 1f),
            new Vector4(0.62f, 0.65f, 0.72f, 1f),
            new Vector4(0.90f, 0.82f, 0.58f, 1f),
        };
        var result = new Vector4[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = palette[i % palette.Length];
        }

        return result;
    }

    private static IReadOnlyList<Matrix4x4> BuildTransientLine(int count, float z, float y, float spacing)
    {
        if (count <= 0)
        {
            return Array.Empty<Matrix4x4>();
        }

        var result = new Matrix4x4[count];
        var xOrigin = -((count - 1) * spacing * 0.5f);
        for (var i = 0; i < count; i++)
        {
            result[i] =
                Matrix4x4.CreateScale(1.8f)
                * Matrix4x4.CreateRotationY(i * 0.17f)
                * Matrix4x4.CreateTranslation(xOrigin + (i * spacing), y, z);
        }

        return result;
    }
}

public sealed record D3D12AlphaFrameDiagnostics(
    string AdapterName,
    int BackBufferWidth,
    int BackBufferHeight,
    int SceneViewportWidth,
    int SceneViewportHeight,
    int SubmittedDrawItems,
    int MapInstanceCount,
    TimeSpan CpuFrameTime)
{
    public bool IsInsideAlphaFrameBudget => CpuFrameTime <= D3D12AlphaFramePlan.AlphaFrameBudget;
}
