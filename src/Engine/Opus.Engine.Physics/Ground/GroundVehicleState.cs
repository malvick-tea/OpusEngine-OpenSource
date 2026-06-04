using System.Numerics;

namespace Opus.Engine.Physics.Ground;

/// <summary>Time-domain state integrated for one powered ground vehicle.</summary>
public readonly record struct GroundVehicleState(
    Vector2 PositionMeters,
    Vector2 VelocityMps,
    float YawRadians,
    float AngularVelocityRadPerSec,
    float EngineRpm,
    int ForwardGearIndex,
    float DistanceTravelledMeters)
{
    public static GroundVehicleState Rest(Vector2 positionMeters = default, float yawRadians = 0f) =>
        new(positionMeters, Vector2.Zero, yawRadians, 0f, 0f, 0, 0f);
}

/// <summary>Normalised driver commands sampled for one fixed integration interval.</summary>
public readonly record struct GroundVehicleControls(float Throttle, float Steering, float Brake = 0f);
