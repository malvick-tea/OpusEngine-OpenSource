using System.Numerics;

namespace Opus.Editor.Ui;

/// <summary>A world-space ray: an origin and a (normalised) direction. Used for viewport picking.</summary>
/// <param name="Origin">World-space start point.</param>
/// <param name="Direction">World-space direction; expected to be unit length.</param>
public readonly record struct Ray(Vector3 Origin, Vector3 Direction)
{
    /// <summary>The point at parametric distance <paramref name="t"/> along the ray.</summary>
    public Vector3 At(float t) => Origin + (Direction * t);
}
