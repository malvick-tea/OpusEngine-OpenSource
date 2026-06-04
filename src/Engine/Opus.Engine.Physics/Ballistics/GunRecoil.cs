namespace Opus.Engine.Physics.Ballistics;

/// <summary>
/// Free-recoil momentum of a gun, from conservation of momentum: when a barrel launches a
/// projectile the gun — and whatever it is mounted to — takes an equal and opposite momentum.
/// </summary>
/// <remarks>
/// The dominant term is the projectile's own momentum <c>m·v</c>. A complete free-recoil figure
/// adds the propellant gas, which leaves the muzzle faster than the shot; it is modelled as a
/// charge mass ejected at <see cref="DefaultGasVelocityFactor"/>× the muzzle velocity. With no
/// charge mass supplied the result collapses to the rigorous projectile-only momentum — a clean
/// lower bound that needs no extra ammunition data, so a roster can recoil correctly from the
/// shell mass and muzzle velocity it already carries, and gain the gas term later per round.
/// The caller divides the momentum by the recoiling mass to get the velocity change and projects
/// it onto the firing axis; this type owns only the impulse magnitude.
/// </remarks>
public static class GunRecoil
{
    /// <summary>Propellant gas leaves the muzzle faster than the shot. 1.5× the muzzle velocity
    /// is the classic free-recoil approximation for the ejected charge. It contributes only when
    /// a non-zero charge mass is supplied, so the projectile-only path is unaffected.</summary>
    public const float DefaultGasVelocityFactor = 1.5f;

    /// <summary>Momentum (kg·m/s) imparted to the gun and its mount by one shot.</summary>
    /// <param name="projectileMassKg">Mass of the launched shot.</param>
    /// <param name="muzzleVelocityMps">Speed of the shot at the muzzle.</param>
    /// <param name="propellantChargeMassKg">Mass of the propellant charge ejected as gas;
    /// zero (the default) yields the projectile-only momentum.</param>
    /// <param name="gasVelocityFactor">Gas exit speed as a multiple of the muzzle velocity.</param>
    public static float FreeRecoilMomentum(
        float projectileMassKg,
        float muzzleVelocityMps,
        float propellantChargeMassKg = 0f,
        float gasVelocityFactor = DefaultGasVelocityFactor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(projectileMassKg);
        ArgumentOutOfRangeException.ThrowIfNegative(muzzleVelocityMps);
        ArgumentOutOfRangeException.ThrowIfNegative(propellantChargeMassKg);
        var projectileMomentum = projectileMassKg * muzzleVelocityMps;
        var gasMomentum = propellantChargeMassKg * gasVelocityFactor * muzzleVelocityMps;
        return projectileMomentum + gasMomentum;
    }

    /// <summary>
    /// Resolves a discharge through the recoiling assembly and the platform's static ground
    /// contact. The recoil brake stretches the impulse over time; the ground can hold part or
    /// all of that load. Only the remainder slides the platform.
    /// </summary>
    public static GunRecoilResponse Solve(GunRecoilProperties properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        Validate(properties);

        var projectileMomentum = properties.ProjectileMassKg * properties.MuzzleVelocityMps;
        var gasMomentum = properties.PropellantChargeMassKg
            * properties.GasVelocityFactor
            * properties.MuzzleVelocityMps
            * (1f - properties.MuzzleBrakeEfficiency);
        var freeMomentum = projectileMomentum + gasMomentum;
        if (freeMomentum <= 0f)
        {
            return default;
        }

        var recoilEnergy = freeMomentum * freeMomentum / (2f * properties.RecoilingAssemblyMassKg);
        var strokeLimitedForce = recoilEnergy / properties.MaximumRecoilTravelMeters;
        var effectiveBrakeForce = MathF.Max(properties.RecoilBrakeForceNewtons, strokeLimitedForce);
        var recoilTravel = recoilEnergy / effectiveBrakeForce;
        var transferDuration = freeMomentum / effectiveBrakeForce;
        var horizontalMomentum = freeMomentum * MathF.Abs(MathF.Cos(properties.BarrelPitchRadians));
        var holdingImpulse = properties.PlatformMassKg
            * properties.GravityMps2
            * properties.GroundStaticFrictionCoefficient
            * transferDuration;
        holdingImpulse = MathF.Min(horizontalMomentum, holdingImpulse);
        var slidingImpulse = horizontalMomentum - holdingImpulse;
        return new GunRecoilResponse(
            freeMomentum,
            horizontalMomentum,
            recoilTravel,
            transferDuration,
            holdingImpulse,
            slidingImpulse,
            slidingImpulse / properties.PlatformMassKg);
    }

    private static void Validate(GunRecoilProperties properties)
    {
        RequireNonNegativeFinite(properties.ProjectileMassKg, nameof(properties.ProjectileMassKg));
        RequireNonNegativeFinite(properties.MuzzleVelocityMps, nameof(properties.MuzzleVelocityMps));
        RequireNonNegativeFinite(properties.PropellantChargeMassKg, nameof(properties.PropellantChargeMassKg));
        RequireNonNegativeFinite(properties.GasVelocityFactor, nameof(properties.GasVelocityFactor));
        RequireUnitInterval(properties.MuzzleBrakeEfficiency, nameof(properties.MuzzleBrakeEfficiency));
        RequireFinite(properties.BarrelPitchRadians, nameof(properties.BarrelPitchRadians));
        RequirePositiveFinite(properties.RecoilingAssemblyMassKg, nameof(properties.RecoilingAssemblyMassKg));
        RequirePositiveFinite(properties.MaximumRecoilTravelMeters, nameof(properties.MaximumRecoilTravelMeters));
        RequirePositiveFinite(properties.RecoilBrakeForceNewtons, nameof(properties.RecoilBrakeForceNewtons));
        RequirePositiveFinite(properties.PlatformMassKg, nameof(properties.PlatformMassKg));
        RequireNonNegativeFinite(
            properties.GroundStaticFrictionCoefficient,
            nameof(properties.GroundStaticFrictionCoefficient));
        RequirePositiveFinite(properties.GravityMps2, nameof(properties.GravityMps2));
    }

    private static void RequireUnitInterval(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0f || value > 1f)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }

    private static void RequirePositiveFinite(float value, string name)
    {
        if (!float.IsFinite(value) || value <= 0f)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }

    private static void RequireNonNegativeFinite(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0f)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }

    private static void RequireFinite(float value, string name)
    {
        if (!float.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}
