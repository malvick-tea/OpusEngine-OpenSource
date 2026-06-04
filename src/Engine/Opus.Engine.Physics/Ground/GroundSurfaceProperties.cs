namespace Opus.Engine.Physics.Ground;

/// <summary>Contact-patch coefficients supplied by the current terrain material.</summary>
public sealed record GroundSurfaceProperties(
    float RollingResistanceMultiplier,
    float LongitudinalFrictionCoefficient,
    float LateralFrictionCoefficient)
{
    public static GroundSurfaceProperties CompactedEarth { get; } = new(1f, 0.72f, 0.68f);

    public static GroundSurfaceProperties DryAsphalt { get; } = new(0.55f, 0.9f, 0.85f);
}
