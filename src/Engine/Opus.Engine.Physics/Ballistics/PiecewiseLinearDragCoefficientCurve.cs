namespace Opus.Engine.Physics.Ballistics;

/// <summary>Interpolated Mach-to-drag table for exterior ballistics and aerodynamics.</summary>
public sealed class PiecewiseLinearDragCoefficientCurve : IDragCoefficientCurve
{
    private readonly DragCoefficientPoint[] _points;

    public PiecewiseLinearDragCoefficientCurve(IEnumerable<DragCoefficientPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        _points = points.OrderBy(point => point.Mach).ToArray();
        if (_points.Length == 0)
        {
            throw new ArgumentException("At least one drag point is required.", nameof(points));
        }

        ValidatePoints();
    }

    public float CoefficientAtMach(float mach)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(mach);
        if (mach <= _points[0].Mach)
        {
            return _points[0].Coefficient;
        }

        for (var i = 1; i < _points.Length; i++)
        {
            if (mach <= _points[i].Mach)
            {
                return Interpolate(_points[i - 1], _points[i], mach);
            }
        }

        return _points[^1].Coefficient;
    }

    private void ValidatePoints()
    {
        for (var i = 0; i < _points.Length; i++)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(_points[i].Mach);
            ArgumentOutOfRangeException.ThrowIfNegative(_points[i].Coefficient);
            if (i > 0 && _points[i - 1].Mach == _points[i].Mach)
            {
                throw new ArgumentException("Drag-curve Mach values must be unique.");
            }
        }
    }

    private static float Interpolate(DragCoefficientPoint left, DragCoefficientPoint right, float mach)
    {
        var t = (mach - left.Mach) / (right.Mach - left.Mach);
        return left.Coefficient + ((right.Coefficient - left.Coefficient) * t);
    }
}

/// <summary>One calibrated sample in a Mach-to-drag table.</summary>
public readonly record struct DragCoefficientPoint(float Mach, float Coefficient);
