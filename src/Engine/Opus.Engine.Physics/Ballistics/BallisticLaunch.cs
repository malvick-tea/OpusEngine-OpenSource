using System.Numerics;

namespace Opus.Engine.Physics.Ballistics;

/// <summary>Converts launch angles and speed into a world-frame velocity.</summary>
public static class BallisticLaunch
{
    public static Vector3 Velocity(float speedMps, float yawRadians, float pitchRadians)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(speedMps);
        var horizontal = speedMps * MathF.Cos(pitchRadians);
        return new Vector3(
            horizontal * MathF.Cos(yawRadians),
            speedMps * MathF.Sin(pitchRadians),
            horizontal * MathF.Sin(yawRadians));
    }
}
