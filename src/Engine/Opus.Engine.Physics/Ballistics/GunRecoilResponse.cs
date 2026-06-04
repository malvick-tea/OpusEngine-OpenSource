namespace Opus.Engine.Physics.Ballistics;

/// <summary>
/// Result of one gun-discharge calculation. Momentum that the ground contact cannot hold
/// becomes platform velocity; recoil travel drives the mount animation independently.
/// </summary>
public readonly record struct GunRecoilResponse(
    float FreeRecoilMomentumKgMetersPerSecond,
    float HorizontalMomentumKgMetersPerSecond,
    float RecoilTravelMeters,
    float TransferDurationSeconds,
    float GroundHoldingImpulseKgMetersPerSecond,
    float SlidingImpulseKgMetersPerSecond,
    float PlatformSpeedChangeMetersPerSecond);
