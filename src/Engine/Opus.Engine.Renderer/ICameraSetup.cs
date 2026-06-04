using System.Numerics;

namespace Opus.Engine.Renderer;

/// <summary>
/// Immutable per-frame camera parameters captured at <see cref="IRenderer.BeginFrame"/>.
/// Once captured, the camera does not change for the rest of the frame — late game-side
/// mutations affect the *next* frame, not the one in flight.
///
/// World convention: right-handed, Y-up. View matrix maps world → view; projection maps
/// view → clip. Reverse-Z depth convention (near plane maps to 1.0, far to 0.0) — better
/// precision distribution, standard in modern engines.
/// </summary>
public readonly record struct CameraSetup(
    Matrix4x4 View,
    Matrix4x4 Projection,
    Vector3 PositionWorld,
    Vector3 ForwardWorld,
    float NearPlane,
    float FarPlane,
    float FovYRadians,
    float AspectRatio);

/// <summary>Multi-camera frame description — main view + optional auxiliary (sniper-scope
/// picture-in-picture, in-world displays, etc.).</summary>
public sealed record FrameCameraSet(
    CameraSetup Main,
    System.Collections.Generic.IReadOnlyList<CameraSetup> Auxiliary)
{
    public static FrameCameraSet SingleMain(CameraSetup main) =>
        new(main, System.Array.Empty<CameraSetup>());
}
