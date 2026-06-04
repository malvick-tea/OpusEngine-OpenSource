using System.Numerics;

namespace Opus.Engine.Physics.Ballistics;

/// <summary>Complete time-domain state for one exterior-ballistics body.</summary>
public readonly record struct BallisticState(
    Vector3 PositionMeters,
    Vector3 VelocityMps,
    float ElapsedSeconds = 0f,
    float DistanceTravelledMeters = 0f);

/// <summary>State after an integration interval plus terminal-condition metadata.</summary>
public readonly record struct BallisticStepResult(BallisticState State, bool HitGround)
{
    public float SpeedMps => State.VelocityMps.Length();
}
