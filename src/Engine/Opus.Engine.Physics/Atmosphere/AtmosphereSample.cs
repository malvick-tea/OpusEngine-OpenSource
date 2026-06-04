using System.Numerics;

namespace Opus.Engine.Physics.Atmosphere;

/// <summary>Atmospheric properties sampled at one altitude.</summary>
public readonly record struct AtmosphereSample(
    float DensityKgPerM3,
    float PressurePascals,
    float TemperatureKelvin,
    float SpeedOfSoundMps,
    Vector3 WindVelocityMps);
