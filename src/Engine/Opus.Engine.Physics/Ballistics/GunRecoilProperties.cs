namespace Opus.Engine.Physics.Ballistics;

/// <summary>
/// Physical inputs for one gun discharge and the platform carrying the mount. The model is
/// intentionally domain-neutral: a tank cannon, a naval mount, and a wheeled self-propelled
/// gun all provide the same quantities and receive the same impulse calculation.
/// </summary>
public sealed record GunRecoilProperties(
    float ProjectileMassKg,
    float MuzzleVelocityMps,
    float PropellantChargeMassKg,
    float GasVelocityFactor,
    float MuzzleBrakeEfficiency,
    float BarrelPitchRadians,
    float RecoilingAssemblyMassKg,
    float MaximumRecoilTravelMeters,
    float RecoilBrakeForceNewtons,
    float PlatformMassKg,
    float GroundStaticFrictionCoefficient,
    float GravityMps2);
