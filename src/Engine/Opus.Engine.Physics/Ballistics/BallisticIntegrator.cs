using System.Numerics;

namespace Opus.Engine.Physics.Ballistics;

/// <summary>
/// Fourth-order Runge-Kutta exterior-ballistics integrator with adaptive fixed-size
/// subdivision. Forces include gravity, altitude-dependent atmosphere, wind-relative
/// velocity, and a caller-provided Mach drag curve.
/// </summary>
public static class BallisticIntegrator
{
    public const float DefaultMaximumSubstepSeconds = 1f / 240f;

    public static BallisticStepResult Advance(
        BallisticState initial,
        BallisticBodyProperties body,
        BallisticEnvironment environment,
        float deltaSeconds,
        float maximumSubstepSeconds = DefaultMaximumSubstepSeconds)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(environment.Atmosphere);
        ArgumentOutOfRangeException.ThrowIfNegative(deltaSeconds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSubstepSeconds);

        var state = initial;
        var remaining = deltaSeconds;
        while (remaining > 0f)
        {
            var step = MathF.Min(remaining, maximumSubstepSeconds);
            var previous = state;
            state = IntegrateRk4(state, body, environment, step);
            state = state with
            {
                ElapsedSeconds = previous.ElapsedSeconds + step,
                DistanceTravelledMeters = previous.DistanceTravelledMeters
                    + Vector3.Distance(previous.PositionMeters, state.PositionMeters),
            };

            if (CrossedGround(previous, state, environment.GroundHeightMeters))
            {
                return new BallisticStepResult(ClampToGround(previous, state, environment.GroundHeightMeters), true);
            }

            remaining -= step;
        }

        return new BallisticStepResult(state, state.PositionMeters.Y <= environment.GroundHeightMeters);
    }

    private static BallisticState IntegrateRk4(
        BallisticState state,
        BallisticBodyProperties body,
        BallisticEnvironment environment,
        float step)
    {
        var k1 = DerivativeAt(state.PositionMeters, state.VelocityMps, body, environment);
        var k2 = DerivativeAt(
            state.PositionMeters + (k1.PositionRate * step * 0.5f),
            state.VelocityMps + (k1.VelocityRate * step * 0.5f),
            body,
            environment);
        var k3 = DerivativeAt(
            state.PositionMeters + (k2.PositionRate * step * 0.5f),
            state.VelocityMps + (k2.VelocityRate * step * 0.5f),
            body,
            environment);
        var k4 = DerivativeAt(
            state.PositionMeters + (k3.PositionRate * step),
            state.VelocityMps + (k3.VelocityRate * step),
            body,
            environment);

        return state with
        {
            PositionMeters = state.PositionMeters + Weighted(k1.PositionRate, k2.PositionRate, k3.PositionRate, k4.PositionRate, step),
            VelocityMps = state.VelocityMps + Weighted(k1.VelocityRate, k2.VelocityRate, k3.VelocityRate, k4.VelocityRate, step),
        };
    }

    private static Derivative DerivativeAt(
        Vector3 position,
        Vector3 velocity,
        BallisticBodyProperties body,
        BallisticEnvironment environment)
    {
        var atmosphere = environment.Atmosphere.Sample(position.Y);
        var relativeVelocity = velocity - atmosphere.WindVelocityMps;
        var relativeSpeed = relativeVelocity.Length();
        if (relativeSpeed <= float.Epsilon || body.ReferenceAreaSquareMeters == 0f)
        {
            return new Derivative(velocity, environment.GravityMps2);
        }

        var mach = relativeSpeed / atmosphere.SpeedOfSoundMps;
        var coefficient = body.DragCurve.CoefficientAtMach(mach);
        var dragForce = 0.5f * atmosphere.DensityKgPerM3 * coefficient
            * body.ReferenceAreaSquareMeters * relativeSpeed * relativeSpeed;
        var dragAcceleration = -(relativeVelocity / relativeSpeed) * (dragForce / body.MassKg);
        return new Derivative(velocity, environment.GravityMps2 + dragAcceleration);
    }

    private static Vector3 Weighted(Vector3 k1, Vector3 k2, Vector3 k3, Vector3 k4, float step) =>
        (k1 + (2f * k2) + (2f * k3) + k4) * (step / 6f);

    private static bool CrossedGround(BallisticState previous, BallisticState current, float ground) =>
        previous.PositionMeters.Y > ground && current.PositionMeters.Y <= ground;

    private static BallisticState ClampToGround(BallisticState previous, BallisticState current, float ground)
    {
        var totalDrop = previous.PositionMeters.Y - current.PositionMeters.Y;
        var t = totalDrop <= float.Epsilon ? 1f : (previous.PositionMeters.Y - ground) / totalDrop;
        var position = Vector3.Lerp(previous.PositionMeters, current.PositionMeters, t);
        position.Y = ground;
        return current with
        {
            PositionMeters = position,
            VelocityMps = Vector3.Lerp(previous.VelocityMps, current.VelocityMps, t),
            ElapsedSeconds = Lerp(previous.ElapsedSeconds, current.ElapsedSeconds, t),
            DistanceTravelledMeters = Lerp(previous.DistanceTravelledMeters, current.DistanceTravelledMeters, t),
        };
    }

    private static float Lerp(float left, float right, float t) => left + ((right - left) * t);

    private readonly record struct Derivative(Vector3 PositionRate, Vector3 VelocityRate);
}
