using System;
using FluentAssertions;
using Opus.Engine.Physics.Destruction;
using Xunit;

namespace Opus.Engine.Physics.Tests.Destruction;

public sealed class PropResistanceTests
{
    [Fact]
    public void Section_modulus_follows_the_circular_formula()
    {
        // Z = π·d³/32.
        PropResistance.CircularSectionModulus(0.3f)
            .Should().BeApproximately(MathF.PI * 0.3f * 0.3f * 0.3f / 32f, 1e-9f);
    }

    [Fact]
    public void Topple_energy_is_rupture_moment_through_the_failure_deflection()
    {
        var modulus = PropResistance.CircularSectionModulus(0.3f);
        PropResistance.ToppleEnergyJoules(0.3f, 40e6f, 0.05f)
            .Should().BeApproximately(40e6f * modulus * 0.05f, 1f);
    }

    [Fact]
    public void Doubling_trunk_diameter_makes_it_eight_times_harder_to_fell()
    {
        var thin = PropResistance.ToppleEnergyJoules(0.3f, 40e6f, 0.05f);
        var thick = PropResistance.ToppleEnergyJoules(0.6f, 40e6f, 0.05f);

        (thick / thin).Should().BeApproximately(8f, 1e-3f);
    }

    [Fact]
    public void A_charging_medium_tank_fells_a_typical_tree_a_crawling_one_does_not()
    {
        // 0.3 m green-wood trunk: σ ≈ 40 MPa, φ ≈ 0.05 rad.
        var topple = PropResistance.ToppleEnergyJoules(0.3f, 40e6f, 0.05f);

        PropResistance.KineticEnergyJoules(25_000f, 2.0f).Should().BeGreaterThan(topple);
        PropResistance.KineticEnergyJoules(25_000f, 0.5f).Should().BeLessThan(topple);
    }

    [Theory]
    [InlineData(-1f, 40e6f, 0.05f)]
    [InlineData(0.3f, -1f, 0.05f)]
    [InlineData(0.3f, 40e6f, -1f)]
    public void Negative_inputs_are_rejected(float diameter, float modulus, float deflection)
    {
        var act = () => PropResistance.ToppleEnergyJoules(diameter, modulus, deflection);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
