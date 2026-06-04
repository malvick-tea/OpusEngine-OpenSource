using System;
using System.Numerics;
using Opus.Foundation.Geometry;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>Owns the Garage scene's orbit camera state: the base framing values derived
/// from the scene AABB, the runtime zoom / pitch offsets, and the critical-damped lerp
/// that smoothly tracks a moving target. Pure with respect to the GPU — exposes
/// <see cref="Current"/> as the camera the render pass consumes, no D3D12 calls inside.
/// </summary>
/// <remarks>
/// Extracted from <see cref="GarageSceneController"/> in M4.u so the controller stops
/// being responsible for both camera arithmetic and render orchestration. Each side now
/// changes for one reason: tune camera feel here, change render composition there.
/// </remarks>
public sealed class GarageCameraController
{
    /// <summary>Multiplicative zoom step per <see cref="Zoom"/> tick.</summary>
    public const float ZoomStepFactor = 1.15f;

    public const float MinZoomFraction = 0.4f;

    public const float MaxZoomFraction = 4.0f;

    /// <summary>Metres of camera elevation change per pixel of vertical mouse drag.</summary>
    public const float PitchMetersPerPixel = 0.03f;

    public const float MinPitchFraction = 0.2f;

    public const float MaxPitchFraction = 6.0f;

    /// <summary>Critical-damped response rate (1/seconds) for the follow lerp. 6.0 reaches
    /// ~95 % of the target within 0.5 s.</summary>
    public const float CameraFollowResponseRate = 6f;

    private readonly Vector3 _baseSceneCentre;
    private readonly float _baseRadius;
    private readonly float _baseHeight;

    private OrbitCamera _camera;
    private Vector3 _smoothedFollowOffset;
    private bool _smoothedFollowInitialised;

    public GarageCameraController(Aabb sceneBounds, int viewportWidth, int viewportHeight)
    {
        _camera = OrbitCamera.From(sceneBounds, viewportWidth, viewportHeight);
        _baseSceneCentre = _camera.Centre;
        _baseRadius = _camera.Radius;
        _baseHeight = _camera.Height;
    }

    /// <summary>Camera state ready to feed to the render pass — base orbit framing
    /// adjusted by zoom + pitch, then re-centred on the smoothed follow target.</summary>
    public OrbitCamera Current => _smoothedFollowInitialised
        ? _camera.WithCentre(_baseSceneCentre + _smoothedFollowOffset)
        : _camera;

    public float BaseRadius => _baseRadius;

    public float BaseHeight => _baseHeight;

    /// <summary>Dollies the orbit radius in / out by <c>ZoomStepFactor ^ -ticks</c>,
    /// clamped to <c>[MinZoomFraction, MaxZoomFraction] × baseRadius</c>.</summary>
    public void Zoom(float ticks)
    {
        if (!IsFiniteNonZero(ticks))
        {
            return;
        }

        var next = _camera.Radius * MathF.Pow(ZoomStepFactor, -ticks);
        var clamped = Math.Clamp(next, _baseRadius * MinZoomFraction, _baseRadius * MaxZoomFraction);
        _camera = _camera.WithRadius(clamped);
    }

    /// <summary>Raises / lowers the camera elevation by <c>deltaPixels × PitchMetersPerPixel</c>,
    /// clamped to <c>[MinPitchFraction, MaxPitchFraction] × baseHeight</c>.</summary>
    public void Pitch(float deltaPixels)
    {
        if (!IsFiniteNonZero(deltaPixels))
        {
            return;
        }

        var next = _camera.Height + (deltaPixels * PitchMetersPerPixel);
        var clamped = Math.Clamp(next, _baseHeight * MinPitchFraction, _baseHeight * MaxPitchFraction);
        _camera = _camera.WithHeight(clamped);
    }

    /// <summary>Advances the follow-lerp toward <paramref name="target"/> over
    /// <paramref name="deltaSeconds"/> using a critical-damped step. The first call after
    /// construction snaps to the target without lerping so there's no startup jolt.</summary>
    public void Advance(float deltaSeconds, Vector3 target)
    {
        if (!_smoothedFollowInitialised)
        {
            _smoothedFollowOffset = target;
            _smoothedFollowInitialised = true;
            return;
        }

        var dt = MathF.Max(deltaSeconds, 0f);
        var t = 1f - MathF.Exp(-CameraFollowResponseRate * dt);
        _smoothedFollowOffset = Vector3.Lerp(_smoothedFollowOffset, target, t);
    }

    private static bool IsFiniteNonZero(float value) =>
        value != 0f && !float.IsNaN(value) && !float.IsInfinity(value);
}
