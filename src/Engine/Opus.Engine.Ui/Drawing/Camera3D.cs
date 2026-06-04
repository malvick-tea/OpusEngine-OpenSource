using System.Numerics;

namespace Opus.Engine.Ui;

/// <summary>
/// Backend-agnostic camera description. Right-handed Y-up, perspective only for now.
/// FOV is vertical, in degrees, matching Raylib + most 3D APIs.
/// </summary>
public readonly record struct CameraView3D(
    Vector3 Position,
    Vector3 Target,
    Vector3 Up,
    float FovYDegrees)
{
    public static CameraView3D LookAt(Vector3 from, Vector3 at, float fovY = 45f) =>
        new(from, at, Vector3.UnitY, fovY);
}
