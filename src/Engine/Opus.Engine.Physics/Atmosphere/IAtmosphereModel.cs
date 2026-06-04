namespace Opus.Engine.Physics.Atmosphere;

/// <summary>Supplies altitude-dependent air properties to aerodynamic solvers.</summary>
public interface IAtmosphereModel
{
    AtmosphereSample Sample(float altitudeMeters);
}
