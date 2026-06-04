namespace Opus.Engine.Physics.Ballistics;

/// <summary>Derived values that do not belong in the time-domain ballistic state.</summary>
public static class BallisticMetrics
{
    public static float KineticEnergyJoules(BallisticBodyProperties body, BallisticState state)
    {
        ArgumentNullException.ThrowIfNull(body);
        return 0.5f * body.MassKg * state.VelocityMps.LengthSquared();
    }
}
