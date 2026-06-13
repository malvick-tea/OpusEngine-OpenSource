using System;
using System.Numerics;

namespace Opus.Editor.Ui;

/// <summary>
/// A turntable / orbit camera for the editor viewport: it looks at a target point from a yaw / pitch /
/// distance, and the viewport's mouse handlers drive it through <see cref="Orbit"/>, <see cref="Zoom"/>,
/// and <see cref="Pan"/>. Pure math — it produces an eye position, view / projection matrices, and pick
/// rays; no GPU. Pitch is clamped just shy of vertical so the up vector never degenerates.
/// </summary>
public sealed class OrbitCamera
{
    public const float MinPitchDegrees = -89f;
    public const float MaxPitchDegrees = 89f;
    public const float MinDistance = 0.01f;

    /// <summary>The home view the camera starts at and <see cref="Reset"/> returns to.</summary>
    public const float DefaultDistance = 10f;
    public const float DefaultYawDegrees = 45f;
    public const float DefaultPitchDegrees = 30f;

    private const float Deg2Rad = MathF.PI / 180f;

    public Vector3 Target { get; set; } = Vector3.Zero;

    public float Distance { get; private set; } = DefaultDistance;

    public float YawDegrees { get; private set; } = DefaultYawDegrees;

    public float PitchDegrees { get; private set; } = DefaultPitchDegrees;

    public float FieldOfViewDegrees { get; set; } = 60f;

    public float NearPlane { get; set; } = 0.1f;

    public float FarPlane { get; set; } = 1000f;

    public Vector3 EyePosition => Target + OffsetFromAngles();

    public Matrix4x4 ViewMatrix => Matrix4x4.CreateLookAt(EyePosition, Target, Vector3.UnitY);

    public Matrix4x4 ProjectionMatrix(float aspectRatio) =>
        Matrix4x4.CreatePerspectiveFieldOfView(FieldOfViewDegrees * Deg2Rad, aspectRatio, NearPlane, FarPlane);

    /// <summary>Rotates the eye around the target. Pitch is clamped to keep the view non-degenerate.</summary>
    public void Orbit(float deltaYawDegrees, float deltaPitchDegrees)
    {
        YawDegrees = WrapDegrees(YawDegrees + deltaYawDegrees);
        PitchDegrees = Math.Clamp(PitchDegrees + deltaPitchDegrees, MinPitchDegrees, MaxPitchDegrees);
    }

    /// <summary>Dollies toward (factor &lt; 1) or away from (factor &gt; 1) the target, clamped at
    /// <see cref="MinDistance"/>.</summary>
    public void Zoom(float factor)
    {
        if (factor <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(factor), factor, "Zoom factor must be positive.");
        }

        Distance = MathF.Max(MinDistance, Distance * factor);
    }

    public void SetDistance(float distance) => Distance = MathF.Max(MinDistance, distance);

    /// <summary>Returns the camera to the home view — origin target, default distance and angles (the H
    /// key) — recovering instantly from any disorienting orbit, pan, or zoom.</summary>
    public void Reset()
    {
        Target = Vector3.Zero;
        Distance = DefaultDistance;
        YawDegrees = DefaultYawDegrees;
        PitchDegrees = DefaultPitchDegrees;
    }

    /// <summary>Slides the target in the camera's right / up plane (a screen-space pan).</summary>
    public void Pan(float right, float up)
    {
        Basis(out _, out var rightAxis, out var upAxis);
        Target += (rightAxis * right) + (upAxis * up);
    }

    /// <summary>Builds the pinhole pick ray through a normalised viewport point (0,0 = top-left,
    /// 1,1 = bottom-right). Independent of the projection matrix convention.</summary>
    public Ray PickRay(float viewportX01, float viewportY01, float aspectRatio)
    {
        Basis(out var forward, out var rightAxis, out var upAxis);
        float tanHalfFov = MathF.Tan(0.5f * FieldOfViewDegrees * Deg2Rad);
        float ndcX = ((viewportX01 * 2f) - 1f) * aspectRatio * tanHalfFov;
        float ndcY = (1f - (viewportY01 * 2f)) * tanHalfFov;
        var direction = Vector3.Normalize(forward + (rightAxis * ndcX) + (upAxis * ndcY));
        return new Ray(EyePosition, direction);
    }

    private void Basis(out Vector3 forward, out Vector3 right, out Vector3 up)
    {
        forward = Vector3.Normalize(Target - EyePosition);
        right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        up = Vector3.Cross(right, forward);
    }

    private Vector3 OffsetFromAngles()
    {
        float yaw = YawDegrees * Deg2Rad;
        float pitch = PitchDegrees * Deg2Rad;
        float cosPitch = MathF.Cos(pitch);
        return Distance * new Vector3(cosPitch * MathF.Sin(yaw), MathF.Sin(pitch), cosPitch * MathF.Cos(yaw));
    }

    private static float WrapDegrees(float degrees)
    {
        float wrapped = degrees % 360f;
        return wrapped < 0f ? wrapped + 360f : wrapped;
    }
}
