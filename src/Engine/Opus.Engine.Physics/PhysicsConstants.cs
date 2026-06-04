namespace Opus.Engine.Physics;

/// <summary>Reference values shared by the deterministic physical models.</summary>
public static class PhysicsConstants
{
    public const float StandardGravityMps2 = 9.80665f;
    public const float SeaLevelAirDensityKgPerM3 = 1.225f;
    public const float HorsepowerToWatts = 745.699872f;
    public const float KilometresPerHourToMetresPerSecond = 1000f / 3600f;
    public const float DegreesToRadians = MathF.PI / 180f;
}
