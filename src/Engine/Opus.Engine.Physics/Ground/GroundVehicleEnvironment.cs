using System;
using System.Numerics;

namespace Opus.Engine.Physics.Ground;

/// <summary>Ambient air, gravity, and terrain material for ground-vehicle dynamics.</summary>
public sealed record GroundVehicleEnvironment(
    float AirDensityKgPerM3,
    Vector2 WindVelocityMps,
    float GravityMps2,
    GroundSurfaceProperties Surface)
{
    /// <summary>Optional terrain surface sampler — world (x east, z north) → height (y up). When
    /// set, the integrator resolves the local slope and adds the gravity-along-slope force, so a
    /// vehicle accelerates and slides downhill, climbs slower than it descends, and (parked) grips
    /// a gentle grade instead of floating level. Null (the default) models perfectly flat ground,
    /// leaving every existing caller — and the determinism scenarios — byte-for-byte unchanged.</summary>
    public Func<float, float, float>? SurfaceHeightSampler { get; init; }

    public static GroundVehicleEnvironment EarthCompactedGround { get; } = new(
        PhysicsConstants.SeaLevelAirDensityKgPerM3,
        Vector2.Zero,
        PhysicsConstants.StandardGravityMps2,
        GroundSurfaceProperties.CompactedEarth);
}
