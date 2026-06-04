using System.Numerics;

namespace Opus.Engine.Physics.Ground;

/// <summary>
/// Semi-implicit deterministic ground-vehicle solver. Longitudinal acceleration emerges
/// from torque, gearing, power, contact traction, rolling resistance, aerodynamic drag,
/// and braking. Lateral contact force and yaw torque model steering and skid resistance.
/// </summary>
/// <remarks>
/// Terrain coupling is exact: the weight pressed onto the contact patch is
/// <c>m·g·cos θ</c>, not <c>m·g</c>, so on a grade the patch carries less normal load and
/// therefore less traction, rolling drag, lateral grip, and steering authority — a hull
/// accelerates worse uphill, slides on a steep cross-slope, cannot power up a grade steeper
/// than its friction angle, and pivots slower on a hill. On flat ground (no height sampler,
/// or a locally level patch) the slope is zero, <c>cos θ = 1</c> exactly, and every force is
/// bit-for-bit identical to the flat model — so the determinism scenarios are unaffected.
/// </remarks>
public static class GroundVehicleIntegrator
{
    public const float DefaultMaximumSubstepSeconds = 1f / 120f;

    /// <summary>Throttle magnitude below which the drivetrain is treated as coasting, so
    /// engine braking engages. With digital throttle (key down / up) this is a clean gate;
    /// it also tolerates a small analogue dead zone.</summary>
    private const float OffThrottleDeadband = 0.05f;

    /// <summary>Speed-squared below which an un-driven vehicle is treated as parked, so static
    /// grip (not kinetic sliding) decides whether it holds the slope. 0.25 ⇒ ≤ 0.5 m/s.</summary>
    private const float ParkedSpeedSquared = 0.25f;

    public static GroundVehicleState Advance(
        GroundVehicleState initial,
        GroundVehicleProperties vehicle,
        GroundVehicleEnvironment environment,
        GroundVehicleControls controls,
        float deltaSeconds,
        float maximumSubstepSeconds = DefaultMaximumSubstepSeconds)
    {
        ArgumentNullException.ThrowIfNull(vehicle);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(environment.Surface);
        ArgumentOutOfRangeException.ThrowIfNegative(deltaSeconds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSubstepSeconds);

        var state = initial;
        var remaining = deltaSeconds;
        while (remaining > 0f)
        {
            var step = MathF.Min(remaining, maximumSubstepSeconds);
            state = AdvanceSubstep(state, vehicle, environment, controls, step);
            remaining -= step;
        }

        return state;
    }

    private static GroundVehicleState AdvanceSubstep(
        GroundVehicleState state,
        GroundVehicleProperties vehicle,
        GroundVehicleEnvironment environment,
        GroundVehicleControls controls,
        float step)
    {
        var throttle = Math.Clamp(controls.Throttle, -1f, 1f);
        var steering = Math.Clamp(controls.Steering, -1f, 1f);
        var brake = Math.Clamp(controls.Brake, 0f, 1f);
        var forward = new Vector2(MathF.Cos(state.YawRadians), MathF.Sin(state.YawRadians));
        var right = new Vector2(-forward.Y, forward.X);
        var signedForwardSpeed = Vector2.Dot(state.VelocityMps, forward);
        var powertrain = PowertrainSolver.Evaluate(vehicle.Powertrain, signedForwardSpeed, throttle, state.ForwardGearIndex);
        var slope = SampleSlope(state, vehicle, environment);
        var slopeCosine = SlopeCosine(slope);
        var contact = ContactForces(
            state, vehicle, environment, forward, right, powertrain.DrivenForceNewtons, throttle, brake, step, slope, slopeCosine);
        var acceleration = contact / vehicle.MassKg;
        var velocity = state.VelocityMps + (acceleration * step);
        velocity = RemoveIdleCreep(velocity, contact, throttle, brake);

        // The yaw moment a tracked hull generates is differential track friction, so its authority
        // scales with the normal load on the patch — a hull pivots slower on a grade than on the flat.
        var steeringTorque = steering * vehicle.MaximumSteeringTorqueNewtonMeters * slopeCosine;
        var dampingTorque = -state.AngularVelocityRadPerSec * vehicle.AngularDampingNewtonMeterSeconds;
        var angularVelocity = state.AngularVelocityRadPerSec
            + (((steeringTorque + dampingTorque) / vehicle.YawInertiaKgSquareMeters) * step);
        var yaw = state.YawRadians + (angularVelocity * step);
        var position = state.PositionMeters + (velocity * step);
        return state with
        {
            PositionMeters = position,
            VelocityMps = velocity,
            YawRadians = yaw,
            AngularVelocityRadPerSec = angularVelocity,
            EngineRpm = powertrain.EngineRpm,
            ForwardGearIndex = powertrain.ForwardGearIndex,
            DistanceTravelledMeters = state.DistanceTravelledMeters + velocity.Length() * step,
        };
    }

    private static Vector2 ContactForces(
        GroundVehicleState state,
        GroundVehicleProperties vehicle,
        GroundVehicleEnvironment environment,
        Vector2 forward,
        Vector2 right,
        float drivenForce,
        float throttle,
        float brake,
        float step,
        SlopeSample slope,
        float slopeCosine)
    {
        // Weight (gravity) is full; the share pressed onto the contact patch — and therefore every
        // friction-limited force — reduces with the grade by cos θ. Flat ground ⇒ cos θ = 1 ⇒ no change.
        var weightForce = vehicle.MassKg * environment.GravityMps2;
        var normalForce = weightForce * slopeCosine;
        var maximumLongitudinal = normalForce * environment.Surface.LongitudinalFrictionCoefficient * vehicle.TractionScale;
        var maximumLateral = normalForce * environment.Surface.LateralFrictionCoefficient * vehicle.LateralGripScale;
        var drive = forward * Math.Clamp(drivenForce, -maximumLongitudinal, maximumLongitudinal);
        var rolling = RollingResistance(state.VelocityMps, normalForce, vehicle, environment, step);
        var aerodynamic = AerodynamicDrag(state.VelocityMps, vehicle, environment);
        var engineBraking = EngineBraking(state.VelocityMps, forward, throttle, vehicle, step);
        var braking = BrakingForce(state.VelocityMps, vehicle.MaximumBrakeForceNewtons * brake, step, vehicle.MassKg);
        var lateral = LateralGrip(state.VelocityMps, right, maximumLateral, vehicle, step);
        var turningResistance = TurningResistance(
            state.VelocityMps,
            state.AngularVelocityRadPerSec,
            normalForce,
            vehicle,
            step);
        var contactPatch = ClampContactPatchForce(
            drive + engineBraking + braking + lateral + turningResistance,
            forward,
            right,
            maximumLongitudinal,
            maximumLateral);
        var slopeForce = SlopeForce(state, vehicle, environment, weightForce, throttle, slope);
        return contactPatch + rolling + aerodynamic + slopeForce;
    }

    /// <summary>Clamps the driven, braking, and lateral forces to one friction ellipse. Without
    /// this shared budget a vehicle can consume full forward traction and full side grip at once,
    /// which lets a heavy hull carve a powered corner like a racing car. Separate longitudinal
    /// and lateral radii preserve the surface's directional coefficients while keeping the total
    /// contact demand physically bounded.</summary>
    private static Vector2 ClampContactPatchForce(
        Vector2 force,
        Vector2 forward,
        Vector2 right,
        float maximumLongitudinal,
        float maximumLateral)
    {
        var longitudinal = Math.Clamp(Vector2.Dot(force, forward), -maximumLongitudinal, maximumLongitudinal);
        var lateral = Math.Clamp(Vector2.Dot(force, right), -maximumLateral, maximumLateral);
        if (maximumLongitudinal <= float.Epsilon)
        {
            return right * lateral;
        }

        if (maximumLateral <= float.Epsilon)
        {
            return forward * longitudinal;
        }

        var longitudinalShare = longitudinal / maximumLongitudinal;
        var lateralShare = lateral / maximumLateral;
        var demandSquared = (longitudinalShare * longitudinalShare) + (lateralShare * lateralShare);
        if (demandSquared <= 1f)
        {
            return (forward * longitudinal) + (right * lateral);
        }

        var scale = 1f / MathF.Sqrt(demandSquared);
        return (forward * longitudinal * scale) + (right * lateral * scale);
    }

    /// <summary>Central-difference terrain gradient under the contact patch (world x east, z north).
    /// Returned once per substep and shared by the slope force and the normal-load reduction so the
    /// height field is sampled a single time. No sampler (flat world) returns <see cref="SlopeSample.Flat"/>.</summary>
    private static SlopeSample SampleSlope(
        GroundVehicleState state,
        GroundVehicleProperties vehicle,
        GroundVehicleEnvironment environment)
    {
        var sampler = environment.SurfaceHeightSampler;
        if (sampler is null)
        {
            return SlopeSample.Flat;
        }

        var x = state.PositionMeters.X;
        var z = state.PositionMeters.Y;
        var d = vehicle.TerrainSlopeSampleDistanceMeters;
        var gradientX = (sampler(x + d, z) - sampler(x - d, z)) / (2f * d);
        var gradientZ = (sampler(x, z + d) - sampler(x, z - d)) / (2f * d);
        return new SlopeSample(gradientX, gradientZ, (gradientX * gradientX) + (gradientZ * gradientZ));
    }

    /// <summary>cos θ of the local grade from the gradient magnitude (|∇h| = tan θ, so
    /// cos θ = 1/√(1 + tan²θ)). Exactly 1 on level ground, keeping the flat model unchanged.</summary>
    private static float SlopeCosine(SlopeSample slope) =>
        slope.GradientSquared <= 0f ? 1f : 1f / MathF.Sqrt(1f + slope.GradientSquared);

    /// <summary>Gravity resolved along the terrain surface: the horizontal force that makes a
    /// vehicle accelerate and slide downhill, climb slower than it descends, and slip on a
    /// cross-slope (the lateral grip then resists that component up to its friction limit). The
    /// gradient is the one sampled by <see cref="SampleSlope"/>; with no sampler (flat ground) the
    /// force is exactly zero, so a flat-world caller is bit-for-bit unchanged. A parked, un-driven
    /// vehicle grips a grade up to the longitudinal friction limit (≈ tan θ ≤ μ), so it holds a
    /// drivable hill instead of creeping off it.</summary>
    private static Vector2 SlopeForce(
        GroundVehicleState state,
        GroundVehicleProperties vehicle,
        GroundVehicleEnvironment environment,
        float weightForce,
        float throttle,
        SlopeSample slope)
    {
        if (slope.GradientSquared <= float.Epsilon)
        {
            return Vector2.Zero;
        }

        // A parked, un-driven hull grips while the grade stays within the friction angle: static
        // friction μ·m·g·cos θ resists the down-slope pull m·g·sin θ exactly when tan θ = |∇h| ≤ μ.
        // Comparing the gradient to μ directly is the clean form (the cos θ cancels on both sides).
        var parked = state.VelocityMps.LengthSquared() <= ParkedSpeedSquared
            && MathF.Abs(throttle) <= OffThrottleDeadband;
        var gripGradient = environment.Surface.LongitudinalFrictionCoefficient * vehicle.TractionScale;
        if (parked && slope.GradientSquared <= gripGradient * gripGradient)
        {
            return Vector2.Zero;
        }

        return new Vector2(-slope.GradientX, -slope.GradientZ) * (weightForce / (1f + slope.GradientSquared));
    }

    /// <summary>Off-throttle drivetrain drag (engine braking). When the driver lifts off the
    /// throttle the engine, transmission, and tracks impose a viscous retarding force along the
    /// longitudinal axis, proportional to forward speed. It is gated to the coasting regime so it
    /// never robs a throttling vehicle of top speed, and clamped so it decays to a stop instead of
    /// reversing the hull through zero in a single substep.</summary>
    private static Vector2 EngineBraking(
        Vector2 velocity,
        Vector2 forward,
        float throttle,
        GroundVehicleProperties vehicle,
        float step)
    {
        if (vehicle.EngineBrakingCoefficientNsPerM <= 0f || MathF.Abs(throttle) > OffThrottleDeadband)
        {
            return Vector2.Zero;
        }

        var forwardSpeed = Vector2.Dot(velocity, forward);
        var speed = MathF.Abs(forwardSpeed);
        if (speed <= float.Epsilon)
        {
            return Vector2.Zero;
        }

        var demand = vehicle.EngineBrakingCoefficientNsPerM * speed;
        var stopForce = speed * vehicle.MassKg / step;
        return forward * (-MathF.Sign(forwardSpeed) * MathF.Min(demand, stopForce));
    }

    /// <summary>Planar travel loss caused by skid steering. A rotating tracked contact patch
    /// scrubs against the ground even when its centre-of-mass velocity follows the hull heading;
    /// without this term a tank can keep nearly full cruise through a sustained powered turn.
    /// The force is proportional to yaw rate and clamped to a stop, never a reversal.</summary>
    private static Vector2 TurningResistance(
        Vector2 velocity,
        float angularVelocity,
        float normalForce,
        GroundVehicleProperties vehicle,
        float step)
    {
        var speed = velocity.Length();
        if (speed <= float.Epsilon
            || vehicle.TurningResistanceCoefficientSeconds <= 0f
            || MathF.Abs(angularVelocity) <= float.Epsilon)
        {
            return Vector2.Zero;
        }

        var demand = normalForce * vehicle.TurningResistanceCoefficientSeconds * MathF.Abs(angularVelocity);
        var stopForce = speed * vehicle.MassKg / step;
        return -velocity / speed * MathF.Min(demand, stopForce);
    }

    private static Vector2 RollingResistance(
        Vector2 velocity,
        float normalForce,
        GroundVehicleProperties vehicle,
        GroundVehicleEnvironment environment,
        float step)
    {
        if (velocity.LengthSquared() <= float.Epsilon)
        {
            return Vector2.Zero;
        }

        var magnitude = normalForce * vehicle.RollingResistanceCoefficient
            * environment.Surface.RollingResistanceMultiplier;
        var stopForce = velocity.Length() * vehicle.MassKg / step;
        return -Vector2.Normalize(velocity) * MathF.Min(magnitude, stopForce);
    }

    private static Vector2 AerodynamicDrag(
        Vector2 velocity,
        GroundVehicleProperties vehicle,
        GroundVehicleEnvironment environment)
    {
        var relativeVelocity = velocity - environment.WindVelocityMps;
        var speed = relativeVelocity.Length();
        if (speed <= float.Epsilon)
        {
            return Vector2.Zero;
        }

        var magnitude = 0.5f * environment.AirDensityKgPerM3
            * vehicle.AerodynamicDragCoefficient * vehicle.FrontalAreaSquareMeters * speed * speed;
        return -relativeVelocity / speed * magnitude;
    }

    private static Vector2 BrakingForce(Vector2 velocity, float maximumForce, float step, float mass)
    {
        var speed = velocity.Length();
        if (speed <= float.Epsilon || maximumForce <= 0f)
        {
            return Vector2.Zero;
        }

        var stopForce = speed * mass / step;
        return -velocity / speed * MathF.Min(maximumForce, stopForce);
    }

    private static Vector2 LateralGrip(
        Vector2 velocity,
        Vector2 right,
        float maximumForce,
        GroundVehicleProperties vehicle,
        float step)
    {
        var lateralSpeed = Vector2.Dot(velocity, right);
        var stopForce = -lateralSpeed * vehicle.MassKg / step;
        return right * Math.Clamp(stopForce, -maximumForce, maximumForce);
    }

    private static Vector2 RemoveIdleCreep(Vector2 velocity, Vector2 netForce, float throttle, float brake)
    {
        if (MathF.Abs(throttle) > float.Epsilon || brake > 0f || velocity.LengthSquared() > 0.0001f)
        {
            return velocity;
        }

        return netForce.LengthSquared() < 1f ? Vector2.Zero : velocity;
    }

    /// <summary>Local terrain gradient under the contact patch. <see cref="GradientSquared"/> is the
    /// squared slope magnitude (tan²θ), retained so both the slope force and the normal-load cosine
    /// reuse one height-field sample.</summary>
    private readonly record struct SlopeSample(float GradientX, float GradientZ, float GradientSquared)
    {
        public static SlopeSample Flat => default;
    }
}
