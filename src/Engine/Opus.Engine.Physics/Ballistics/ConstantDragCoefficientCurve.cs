namespace Opus.Engine.Physics.Ballistics;

/// <summary>Mach-independent drag approximation for bodies with sparse source data.</summary>
public sealed class ConstantDragCoefficientCurve : IDragCoefficientCurve
{
    public ConstantDragCoefficientCurve(float coefficient)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(coefficient);
        Coefficient = coefficient;
    }

    public float Coefficient { get; }

    public float CoefficientAtMach(float mach)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(mach);
        return Coefficient;
    }
}
