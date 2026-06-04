namespace Opus.Engine.Physics.Ballistics;

/// <summary>Mass and aerodynamic geometry of a freely flying body.</summary>
public sealed record BallisticBodyProperties
{
    public BallisticBodyProperties(
        float massKg,
        float referenceAreaSquareMeters,
        IDragCoefficientCurve dragCurve)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(massKg);
        ArgumentOutOfRangeException.ThrowIfNegative(referenceAreaSquareMeters);
        ArgumentNullException.ThrowIfNull(dragCurve);
        MassKg = massKg;
        ReferenceAreaSquareMeters = referenceAreaSquareMeters;
        DragCurve = dragCurve;
    }

    public float MassKg { get; }

    public float ReferenceAreaSquareMeters { get; }

    public IDragCoefficientCurve DragCurve { get; }

    public static BallisticBodyProperties FromDiameter(
        float massKg,
        float diameterMeters,
        IDragCoefficientCurve dragCurve)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(diameterMeters);
        var radius = diameterMeters * 0.5f;
        return new BallisticBodyProperties(massKg, MathF.PI * radius * radius, dragCurve);
    }
}
