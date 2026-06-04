using System.Numerics;

namespace Opus.Engine.Consumer.Scene;

/// <summary>Engine-neutral camera parameters for one rendered view.</summary>
public readonly record struct ConsumerCamera(
    Matrix4x4 View,
    Matrix4x4 Projection,
    Vector3 PositionWorld,
    Vector3 ForwardWorld,
    float NearPlane,
    float FarPlane,
    float FovYRadians,
    float AspectRatio);
