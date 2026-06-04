using FluentAssertions;
using Opus.Engine.Physics.Atmosphere;
using Xunit;

namespace Opus.Engine.Physics.Tests.Atmosphere;

public sealed class StandardAtmosphereTests
{
    [Fact]
    public void Sea_level_sample_matches_reference_density()
    {
        var sample = new StandardAtmosphere().Sample(0f);

        sample.DensityKgPerM3.Should().BeApproximately(1.225f, 0.001f);
        sample.PressurePascals.Should().BeApproximately(101325f, 1f);
    }

    [Fact]
    public void Density_and_temperature_fall_with_altitude()
    {
        var atmosphere = new StandardAtmosphere();

        var sea = atmosphere.Sample(0f);
        var high = atmosphere.Sample(5000f);

        high.DensityKgPerM3.Should().BeLessThan(sea.DensityKgPerM3);
        high.TemperatureKelvin.Should().BeLessThan(sea.TemperatureKelvin);
    }
}
