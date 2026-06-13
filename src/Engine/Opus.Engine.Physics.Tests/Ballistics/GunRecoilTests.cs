using System;
using FluentAssertions;
using Opus.Engine.Physics.Ballistics;
using Xunit;

namespace Opus.Engine.Physics.Tests.Ballistics;

public sealed class GunRecoilTests
{
    [Fact]
    public void Projectile_only_recoil_is_the_shot_momentum()
    {
        // 7.5 cm KwK 40 class shot: 6.8 kg at 750 m/s.
        GunRecoil.FreeRecoilMomentum(6.8f, 750f).Should().BeApproximately(6.8f * 750f, 0.001f);
    }

    [Fact]
    public void Propellant_charge_adds_gas_momentum_at_the_gas_velocity_factor()
    {
        var projectileOnly = GunRecoil.FreeRecoilMomentum(6.8f, 750f);
        var withCharge = GunRecoil.FreeRecoilMomentum(6.8f, 750f, propellantChargeMassKg: 2.7f);

        withCharge.Should().BeApproximately(
            projectileOnly + (2.7f * GunRecoil.DefaultGasVelocityFactor * 750f), 0.001f);
        withCharge.Should().BeGreaterThan(projectileOnly);
    }

    [Fact]
    public void Heavier_or_faster_shot_recoils_harder()
    {
        var light = GunRecoil.FreeRecoilMomentum(2.5f, 350f); // 57 mm Type 90 class
        var heavy = GunRecoil.FreeRecoilMomentum(40f, 430f);  // 152 mm M-10 class

        heavy.Should().BeGreaterThan(light);
    }

    [Fact]
    public void Zero_shot_mass_yields_no_recoil()
    {
        GunRecoil.FreeRecoilMomentum(0f, 750f).Should().Be(0f);
    }

    [Theory]
    [InlineData(-1f, 750f, 0f)]
    [InlineData(6.8f, -1f, 0f)]
    [InlineData(6.8f, 750f, -1f)]
    public void Negative_inputs_are_rejected(float massKg, float velocity, float charge)
    {
        var act = () => GunRecoil.FreeRecoilMomentum(massKg, velocity, charge);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Solve_reports_mount_travel_but_no_platform_slide_when_ground_holds_the_discharge()
    {
        var response = GunRecoil.Solve(RepresentativeMountedGunProperties());

        response.RecoilTravelMeters.Should().BeGreaterThan(0f);
        response.GroundHoldingImpulseKgMetersPerSecond.Should().Be(response.HorizontalMomentumKgMetersPerSecond);
        response.SlidingImpulseKgMetersPerSecond.Should().Be(0f);
        response.PlatformSpeedChangeMetersPerSecond.Should().Be(0f);
    }

    [Fact]
    public void Solve_slides_the_platform_when_the_ground_cannot_hold_the_discharge()
    {
        var response = GunRecoil.Solve(RepresentativeMountedGunProperties() with
        {
            GroundStaticFrictionCoefficient = 0.01f,
        });

        response.SlidingImpulseKgMetersPerSecond.Should().BeGreaterThan(0f);
        response.PlatformSpeedChangeMetersPerSecond.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void Solve_accounts_for_gas_and_muzzle_brake_efficiency()
    {
        var unbraked = GunRecoil.Solve(RepresentativeMountedGunProperties());
        var braked = GunRecoil.Solve(RepresentativeMountedGunProperties() with { MuzzleBrakeEfficiency = 0.5f });

        braked.FreeRecoilMomentumKgMetersPerSecond.Should().BeLessThan(unbraked.FreeRecoilMomentumKgMetersPerSecond);
    }

    [Fact]
    public void Solve_rejects_invalid_physical_inputs()
    {
        var act = () => GunRecoil.Solve(RepresentativeMountedGunProperties() with { PlatformMassKg = 0f });

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static GunRecoilProperties RepresentativeMountedGunProperties() => new(
        ProjectileMassKg: 6.8f,
        MuzzleVelocityMps: 750f,
        PropellantChargeMassKg: 2.7f,
        GasVelocityFactor: GunRecoil.DefaultGasVelocityFactor,
        MuzzleBrakeEfficiency: 0f,
        BarrelPitchRadians: 0f,
        RecoilingAssemblyMassKg: 900f,
        MaximumRecoilTravelMeters: 0.5f,
        RecoilBrakeForceNewtons: 100_000f,
        PlatformMassKg: 25_000f,
        GroundStaticFrictionCoefficient: 0.72f,
        GravityMps2: 9.80665f);
}
