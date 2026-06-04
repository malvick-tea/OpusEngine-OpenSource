using System.Numerics;

namespace Opus.Engine.Physics.Atmosphere;

/// <summary>
/// Deterministic International Standard Atmosphere troposphere model. Altitudes outside
/// the 0..11 km layer are clamped because the current model intentionally excludes the
/// stratosphere; callers can provide another <see cref="IAtmosphereModel"/> when needed.
/// </summary>
public sealed class StandardAtmosphere : IAtmosphereModel
{
    public const float MaximumModelAltitudeMeters = 11000f;

    private const float SeaLevelTemperatureKelvin = 288.15f;
    private const float SeaLevelPressurePascals = 101325f;
    private const float TemperatureLapseKelvinPerMeter = 0.0065f;
    private const float SpecificGasConstant = 287.05287f;
    private const float HeatCapacityRatio = 1.4f;
    private readonly Vector3 _windVelocityMps;

    public StandardAtmosphere(Vector3 windVelocityMps = default)
    {
        _windVelocityMps = windVelocityMps;
    }

    public AtmosphereSample Sample(float altitudeMeters)
    {
        var altitude = Math.Clamp(altitudeMeters, 0f, MaximumModelAltitudeMeters);
        var temperature = SeaLevelTemperatureKelvin - (TemperatureLapseKelvinPerMeter * altitude);
        var exponent = PhysicsConstants.StandardGravityMps2
            / (SpecificGasConstant * TemperatureLapseKelvinPerMeter);
        var pressure = SeaLevelPressurePascals
            * MathF.Pow(temperature / SeaLevelTemperatureKelvin, exponent);
        var density = pressure / (SpecificGasConstant * temperature);
        var speedOfSound = MathF.Sqrt(HeatCapacityRatio * SpecificGasConstant * temperature);
        return new AtmosphereSample(density, pressure, temperature, speedOfSound, _windVelocityMps);
    }
}
