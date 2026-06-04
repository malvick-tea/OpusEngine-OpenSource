using System.Numerics;
using Opus.Engine.Physics.Atmosphere;

namespace Opus.Engine.Physics.Ballistics;

/// <summary>External forces and ground plane used by exterior ballistics.</summary>
public sealed record BallisticEnvironment(
    IAtmosphereModel Atmosphere,
    Vector3 GravityMps2,
    float GroundHeightMeters = 0f)
{
    public static BallisticEnvironment EarthSeaLevel { get; } = new(
        new StandardAtmosphere(),
        new Vector3(0f, -PhysicsConstants.StandardGravityMps2, 0f));
}
