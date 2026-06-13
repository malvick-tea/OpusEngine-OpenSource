using System;
using System.Numerics;

namespace Opus.Editor.Core;

/// <summary>
/// A node's local transform in the document model: translation, rotation as Euler angles in degrees, and
/// non-uniform scale. Degrees are kept (rather than a quaternion) so a hand-edited scene file and the
/// pseudo-code mirror read naturally; the render seam converts to a quaternion via
/// <see cref="ToRotationQuaternion"/>.
/// </summary>
/// <param name="Position">Local translation.</param>
/// <param name="RotationEulerDegrees">Local rotation as yaw (Y), pitch (X), roll (Z) in degrees.</param>
/// <param name="Scale">Local non-uniform scale.</param>
public readonly record struct EditorTransform(Float3 Position, Float3 RotationEulerDegrees, Float3 Scale)
{
    public static readonly EditorTransform Identity = new(Float3.Zero, Float3.Zero, Float3.One);

    private const float DegreesToRadians = MathF.PI / 180f;

    public Quaternion ToRotationQuaternion() => Quaternion.CreateFromYawPitchRoll(
        RotationEulerDegrees.Y * DegreesToRadians,
        RotationEulerDegrees.X * DegreesToRadians,
        RotationEulerDegrees.Z * DegreesToRadians);

    public Matrix4x4 ToMatrix()
    {
        return Matrix4x4.CreateScale(Scale.ToVector3())
            * Matrix4x4.CreateFromQuaternion(ToRotationQuaternion())
            * Matrix4x4.CreateTranslation(Position.ToVector3());
    }
}
