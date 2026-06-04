using System.Numerics;
using FluentAssertions;
using Opus.Engine.Physics.Ground;
using Xunit;

namespace Opus.Engine.Physics.Tests.Ground;

public sealed class GroundVehicleIntegratorTests
{
    [Fact]
    public void Throttle_accelerates_vehicle_without_a_speed_cap()
    {
        var state = Simulate(GroundVehicleState.Rest(), Vehicle(12000f), new GroundVehicleControls(1f, 0f), 3f);

        state.PositionMeters.X.Should().BeGreaterThan(0f);
        state.VelocityMps.X.Should().BeGreaterThan(0f);
        state.DistanceTravelledMeters.Should().BeGreaterThan(0f);
        state.EngineRpm.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void Greater_mass_reduces_acceleration_for_same_powertrain()
    {
        var light = Simulate(GroundVehicleState.Rest(), Vehicle(8000f), new GroundVehicleControls(1f, 0f), 1f);
        var heavy = Simulate(GroundVehicleState.Rest(), Vehicle(24000f), new GroundVehicleControls(1f, 0f), 1f);

        heavy.VelocityMps.Length().Should().BeLessThan(light.VelocityMps.Length());
    }

    [Fact]
    public void Steering_changes_yaw_and_angular_velocity()
    {
        var state = Simulate(GroundVehicleState.Rest(), Vehicle(12000f), new GroundVehicleControls(0.5f, 1f), 1f);

        state.YawRadians.Should().BeGreaterThan(0f);
        state.AngularVelocityRadPerSec.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void Drive_and_lateral_grip_share_one_contact_patch_budget()
    {
        const float tick = 1f / 120f;
        const float mass = 12000f;
        const float gravity = 9.81f;
        const float longitudinalCoefficient = 0.72f;
        const float lateralCoefficient = 0.68f;
        var surface = new GroundSurfaceProperties(0f, longitudinalCoefficient, lateralCoefficient);
        var environment = new GroundVehicleEnvironment(0f, Vector2.Zero, gravity, surface);
        var initial = GroundVehicleState.Rest() with { VelocityMps = new Vector2(0f, 5f) };

        var state = GroundVehicleIntegrator.Advance(
            initial,
            Vehicle(mass),
            environment,
            new GroundVehicleControls(1f, 0f),
            tick);

        var force = (state.VelocityMps - initial.VelocityMps) * (mass / tick);
        var longitudinalShare = force.X / (mass * gravity * longitudinalCoefficient);
        var lateralShare = force.Y / (mass * gravity * lateralCoefficient);
        ((longitudinalShare * longitudinalShare) + (lateralShare * lateralShare)).Should()
            .BeLessThanOrEqualTo(1.001f, "powered cornering must stay inside one friction ellipse");
    }

    [Fact]
    public void Turning_resistance_sheds_speed_while_a_contact_patch_scrubs()
    {
        var initial = GroundVehicleState.Rest() with
        {
            VelocityMps = new Vector2(10f, 0f),
            AngularVelocityRadPerSec = 0.5f,
        };

        var free = Simulate(initial, Vehicle(24000f), new GroundVehicleControls(0f, 0f), 1f);
        var scrubbed = Simulate(
            initial,
            Vehicle(24000f, turningResistanceCoefficientSeconds: 0.18f),
            new GroundVehicleControls(0f, 0f),
            1f);

        scrubbed.VelocityMps.Length().Should().BeLessThan(free.VelocityMps.Length());
    }

    [Fact]
    public void Brake_reduces_existing_speed()
    {
        var initial = GroundVehicleState.Rest() with { VelocityMps = new Vector2(10f, 0f) };

        var state = Simulate(initial, Vehicle(12000f), new GroundVehicleControls(0f, 0f, 1f), 0.5f);

        state.VelocityMps.Length().Should().BeLessThan(initial.VelocityMps.Length());
    }

    [Fact]
    public void Engine_braking_sheds_coasting_speed_faster_than_a_free_wheeling_coast()
    {
        var initial = GroundVehicleState.Rest() with { VelocityMps = new Vector2(10f, 0f) };
        var freeWheeling = Simulate(initial, Vehicle(24000f), new GroundVehicleControls(0f, 0f), 3f);
        var braked = Simulate(initial, Vehicle(24000f, engineBrakingCoefficientNsPerM: 4320f), new GroundVehicleControls(0f, 0f), 3f);

        braked.VelocityMps.Length().Should().BeLessThan(freeWheeling.VelocityMps.Length());
    }

    [Fact]
    public void Engine_braking_does_not_cap_throttled_top_speed()
    {
        var braked = Simulate(GroundVehicleState.Rest(), Vehicle(24000f, engineBrakingCoefficientNsPerM: 4320f), new GroundVehicleControls(1f, 0f), 30f);
        var free = Simulate(GroundVehicleState.Rest(), Vehicle(24000f), new GroundVehicleControls(1f, 0f), 30f);

        braked.VelocityMps.Length().Should().BeApproximately(free.VelocityMps.Length(), 0.01f);
    }

    [Fact]
    public void On_a_steep_slope_an_idle_tank_slides_downhill()
    {
        // Height climbs 1 m per metre toward +X — a 45-degree grade (tan 1 > μ≈0.72), so gravity
        // beats track grip and the un-driven hull slides down toward −X.
        var slope = GroundVehicleEnvironment.EarthCompactedGround with { SurfaceHeightSampler = (x, _) => x };

        var state = Simulate(GroundVehicleState.Rest(), Vehicle(24000f), slope, new GroundVehicleControls(0f, 0f), 3f);

        state.PositionMeters.X.Should().BeLessThan(-1f, "a 45-degree slope overcomes static grip");
    }

    [Fact]
    public void On_a_gentle_slope_a_parked_tank_grips_and_holds()
    {
        // A 0.1 grade (~5.7 degrees, tan 0.1 < μ) is well within the friction angle, so a parked
        // hull holds station instead of creeping off the hill.
        var slope = GroundVehicleEnvironment.EarthCompactedGround with { SurfaceHeightSampler = (x, _) => 0.1f * x };

        var state = Simulate(GroundVehicleState.Rest(), Vehicle(24000f), slope, new GroundVehicleControls(0f, 0f), 3f);

        state.PositionMeters.Length().Should().BeLessThan(0.05f, "track grip holds a drivable grade");
    }

    [Fact]
    public void A_tank_already_rolling_keeps_sliding_on_a_grade_that_would_hold_it_parked()
    {
        // Same gentle grade that holds at rest — but grip is static: once the hull is rolling, the
        // grade keeps pulling it downhill (kinetic), so it must not freeze in place.
        var slope = GroundVehicleEnvironment.EarthCompactedGround with { SurfaceHeightSampler = (x, _) => 0.1f * x };
        var rolling = GroundVehicleState.Rest() with { VelocityMps = new Vector2(-1f, 0f) };

        var state = Simulate(rolling, Vehicle(24000f), slope, new GroundVehicleControls(0f, 0f), 3f);

        state.PositionMeters.X.Should().BeLessThan(-2f, "kinetic — a rolling hull keeps descending the grade");
    }

    [Fact]
    public void A_strong_hull_is_traction_limited_so_a_grade_past_its_friction_angle_defeats_full_power()
    {
        // tan 50° ≈ 1.19 > μ (0.72): the down-slope pull exceeds what the patch can grip at the
        // reduced (cos θ) normal load, so even an over-powered hull makes no net headway uphill.
        // Without the cos-θ load reduction the patch would keep full grip and wrongly haul it up.
        var cliff = GroundVehicleEnvironment.EarthCompactedGround with { SurfaceHeightSampler = (x, _) => 1.19f * x };
        var strong = Vehicle(12000f, peakTorqueNewtonMeters: 6000f);

        var state = Simulate(GroundVehicleState.Rest(), strong, cliff, new GroundVehicleControls(1f, 0f), 4f);

        state.PositionMeters.X.Should().BeLessThanOrEqualTo(0.5f, "a grade past the friction angle cannot be powered up");
    }

    [Fact]
    public void The_same_strong_hull_climbs_a_grade_within_its_friction_angle()
    {
        // tan 25° ≈ 0.47 < μ: the identical over-powered hull pulls itself up a drivable grade,
        // proving the previous test fails on the slope, not on the vehicle.
        var grade = GroundVehicleEnvironment.EarthCompactedGround with { SurfaceHeightSampler = (x, _) => 0.47f * x };
        var strong = Vehicle(12000f, peakTorqueNewtonMeters: 6000f);

        var state = Simulate(GroundVehicleState.Rest(), strong, grade, new GroundVehicleControls(1f, 0f), 4f);

        state.PositionMeters.X.Should().BeGreaterThan(3f, "a grade within the friction angle is climbable");
    }

    [Fact]
    public void Steering_authority_falls_off_on_a_slope()
    {
        // The yaw moment is differential track friction, so it scales with normal load: the same
        // steering input turns the hull less on a 30° grade than on the flat.
        var slope = GroundVehicleEnvironment.EarthCompactedGround with { SurfaceHeightSampler = (x, _) => 0.577f * x };
        var vehicle = Vehicle(12000f);
        var controls = new GroundVehicleControls(0.3f, 1f);

        var flat = Simulate(GroundVehicleState.Rest(), vehicle, GroundVehicleEnvironment.EarthCompactedGround, controls, 0.5f);
        var onGrade = Simulate(GroundVehicleState.Rest(), vehicle, slope, controls, 0.5f);

        MathF.Abs(onGrade.AngularVelocityRadPerSec).Should()
            .BeLessThan(MathF.Abs(flat.AngularVelocityRadPerSec), "less normal load means less yaw authority");
    }

    [Fact]
    public void Aerodynamic_and_rolling_losses_bound_long_run_speed()
    {
        var vehicle = Vehicle(12000f);
        var first = Simulate(GroundVehicleState.Rest(), vehicle, new GroundVehicleControls(1f, 0f), 90f);
        var second = Simulate(first, vehicle, new GroundVehicleControls(1f, 0f), 30f);

        second.VelocityMps.Length().Should().BeGreaterThan(0f);
        (second.VelocityMps.Length() - first.VelocityMps.Length()).Should().BeLessThan(3f);
    }

    [Fact]
    public void Terrain_slope_sample_distance_is_configurable_per_vehicle()
    {
        var vehicle = Vehicle(12000f, terrainSlopeSampleDistanceMeters: 3.5f);

        vehicle.TerrainSlopeSampleDistanceMeters.Should().Be(3.5f);
    }

    private static GroundVehicleState Simulate(
        GroundVehicleState state,
        GroundVehicleProperties vehicle,
        GroundVehicleControls controls,
        float seconds) =>
        Simulate(state, vehicle, GroundVehicleEnvironment.EarthCompactedGround, controls, seconds);

    private static GroundVehicleState Simulate(
        GroundVehicleState state,
        GroundVehicleProperties vehicle,
        GroundVehicleEnvironment environment,
        GroundVehicleControls controls,
        float seconds)
    {
        const float tick = 1f / 60f;
        for (var elapsed = 0f; elapsed < seconds; elapsed += tick)
        {
            state = GroundVehicleIntegrator.Advance(state, vehicle, environment, controls, tick);
        }

        return state;
    }

    private static GroundVehicleProperties Vehicle(
        float massKg,
        float engineBrakingCoefficientNsPerM = 0f,
        float turningResistanceCoefficientSeconds = 0f,
        float terrainSlopeSampleDistanceMeters = 2f,
        float peakTorqueNewtonMeters = 900f) => new(
        massKg,
        frontalAreaSquareMeters: 6f,
        aerodynamicDragCoefficient: 0.8f,
        rollingResistanceCoefficient: 0.025f,
        tractionScale: 1f,
        lateralGripScale: 1f,
        maximumBrakeForceNewtons: massKg * 5f,
        yawInertiaKgSquareMeters: massKg * 3f,
        maximumSteeringTorqueNewtonMeters: massKg * 6f,
        angularDampingNewtonMeterSeconds: massKg * 1.5f,
        PowertrainSolverTests.Powertrain(peakTorqueNewtonMeters),
        engineBrakingCoefficientNsPerM,
        turningResistanceCoefficientSeconds,
        terrainSlopeSampleDistanceMeters);
}
