using System.Numerics;
using FluentAssertions;
using Opus.Engine.Physics.Atmosphere;
using Opus.Engine.Physics.Ballistics;
using Xunit;

namespace Opus.Engine.Physics.Tests.Ballistics;

public sealed class BallisticIntegratorTests
{
    [Fact]
    public void Launch_velocity_respects_yaw_pitch_and_speed()
    {
        var velocity = BallisticLaunch.Velocity(100f, MathF.PI / 2f, MathF.PI / 6f);

        velocity.X.Should().BeApproximately(0f, 0.001f);
        velocity.Y.Should().BeApproximately(50f, 0.001f);
        velocity.Z.Should().BeApproximately(86.6025f, 0.001f);
        velocity.Length().Should().BeApproximately(100f, 0.001f);
    }

    [Fact]
    public void Vacuum_trajectory_matches_constant_gravity_solution()
    {
        var body = Body(dragCoefficient: 0f);
        var initial = new BallisticState(new Vector3(0f, 100f, 0f), new Vector3(100f, 10f, 0f));

        var result = BallisticIntegrator.Advance(initial, body, Environment(), 1f);

        result.HitGround.Should().BeFalse();
        result.State.PositionMeters.X.Should().BeApproximately(100f, 0.001f);
        result.State.PositionMeters.Y.Should().BeApproximately(105.0967f, 0.001f);
        result.State.VelocityMps.Y.Should().BeApproximately(0.19335f, 0.001f);
    }

    [Fact]
    public void Aerodynamic_drag_reduces_speed_and_kinetic_energy()
    {
        var body = Body(dragCoefficient: 0.35f);
        var initial = new BallisticState(new Vector3(0f, 100f, 0f), new Vector3(750f, 0f, 0f));

        var result = BallisticIntegrator.Advance(initial, body, Environment(), 0.5f);

        result.SpeedMps.Should().BeLessThan(initial.VelocityMps.Length());
        BallisticMetrics.KineticEnergyJoules(body, result.State)
            .Should().BeLessThan(BallisticMetrics.KineticEnergyJoules(body, initial));
        result.State.DistanceTravelledMeters.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void Ground_intersection_is_clamped_to_plane()
    {
        var body = Body(dragCoefficient: 0f);
        var initial = new BallisticState(new Vector3(0f, 1f, 0f), new Vector3(10f, -1f, 0f));

        var result = BallisticIntegrator.Advance(initial, body, Environment(), 1f);

        result.HitGround.Should().BeTrue();
        result.State.PositionMeters.Y.Should().Be(0f);
        result.State.ElapsedSeconds.Should().BeLessThan(1f);
    }

    private static BallisticBodyProperties Body(float dragCoefficient) =>
        BallisticBodyProperties.FromDiameter(6.8f, 0.075f, new ConstantDragCoefficientCurve(dragCoefficient));

    private static BallisticEnvironment Environment() =>
        new(new StandardAtmosphere(), new Vector3(0f, -PhysicsConstants.StandardGravityMps2, 0f));
}
