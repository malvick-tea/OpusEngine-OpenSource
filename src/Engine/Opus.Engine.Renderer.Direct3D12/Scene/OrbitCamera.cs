using System;
using System.Numerics;
using Opus.Foundation.Geometry;

namespace Opus.Engine.Renderer.Direct3D12.Scene;

/// <summary>AABB-driven orbit camera used by the Garage scene + every glTF asset viewer.
/// The camera sweeps a horizontal circle around the bounds centre at a radius scaled to
/// the asset size, looking inward with a fixed FOV and a far plane that scales with the
/// radius so even thin extents leave headroom. <see cref="At"/> takes a 0..1 phase and
/// returns the per-frame matrices — callers ticking from elapsed time can wrap modulo 1.
/// <para>
/// The struct is read-only; mutable phase / animation state lives on the caller (e.g.
/// <see cref="GarageSceneController"/>). This keeps the camera math itself pure and
/// trivially testable.
/// </para></summary>
public readonly struct OrbitCamera
{
    public Vector3 Centre { get; }

    public float Radius { get; }

    public float Height { get; }

    public Matrix4x4 Projection { get; }

    public float NearPlane { get; }

    public float FarPlane { get; }

    public float FovYRadians { get; }

    public float AspectRatio { get; }

    private OrbitCamera(
        Vector3 centre, float radius, float height, Matrix4x4 projection,
        float nearPlane, float farPlane, float fovYRadians, float aspectRatio)
    {
        Centre = centre;
        Radius = radius;
        Height = height;
        Projection = projection;
        NearPlane = nearPlane;
        FarPlane = farPlane;
        FovYRadians = fovYRadians;
        AspectRatio = aspectRatio;
    }

    public static OrbitCamera From(
        Aabb bounds,
        int viewportWidth,
        int viewportHeight,
        float radiusMultiplier = 2.2f,
        float minRadius = 4f,
        float fovYRadians = MathF.PI / 3f,
        float nearPlane = 0.1f,
        float farPlaneRadiusMultiplier = 8f)
    {
        var extents = bounds.Extents;
        var radius = MathF.Max(extents.Length() * radiusMultiplier, minRadius);
        var aspect = viewportWidth / (float)viewportHeight;
        var farPlane = radius * farPlaneRadiusMultiplier;
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(fovYRadians, aspect, nearPlane, farPlane);
        return new OrbitCamera(bounds.Centre, radius, extents.Y * 0.7f, projection, nearPlane, farPlane, fovYRadians, aspect);
    }

    /// <summary>Returns a copy with the orbit centre moved to <paramref name="centre"/>;
    /// every other parameter (radius, height, projection, near/far, FOV, aspect) is
    /// preserved. Lets a host follow a moving target without re-deriving framing from a
    /// fresh AABB each frame.</summary>
    public OrbitCamera WithCentre(Vector3 centre) =>
        new(centre, Radius, Height, Projection, NearPlane, FarPlane, FovYRadians, AspectRatio);

    /// <summary>Returns a copy with the orbit radius set to <paramref name="radius"/> —
    /// the only field projection cares about is the far plane, which we keep stable so
    /// the depth-buffer range doesn't shift with the zoom. Negative / zero / non-finite
    /// radii are silently clamped to a tiny positive number; callers should clamp to
    /// their own min/max range before calling.</summary>
    public OrbitCamera WithRadius(float radius)
    {
        var safe = radius > 0f && !float.IsNaN(radius) && !float.IsInfinity(radius) ? radius : 0.01f;
        return new OrbitCamera(Centre, safe, Height, Projection, NearPlane, FarPlane, FovYRadians, AspectRatio);
    }

    /// <summary>Returns a copy with the camera elevation set to <paramref name="height"/>
    /// metres above the orbit centre. Non-finite inputs are dropped to zero; callers
    /// should clamp to their own min/max range before calling.</summary>
    public OrbitCamera WithHeight(float height)
    {
        var safe = float.IsNaN(height) || float.IsInfinity(height) ? 0f : height;
        return new OrbitCamera(Centre, Radius, safe, Projection, NearPlane, FarPlane, FovYRadians, AspectRatio);
    }

    /// <summary>Returns the camera state for cycle phase <paramref name="phase"/> in [0, 1).
    /// One full revolution corresponds to <paramref name="phase"/> crossing 1.0.</summary>
    public (Vector3 CameraPos, Matrix4x4 View, Matrix4x4 ViewProjection) At(float phase)
    {
        var angle = phase * MathF.PI * 2f;
        var cameraPos = Centre + new Vector3(
            MathF.Sin(angle) * Radius,
            Height,
            MathF.Cos(angle) * Radius);
        var view = Matrix4x4.CreateLookAt(cameraPos, Centre, Vector3.UnitY);
        return (cameraPos, view, view * Projection);
    }
}
