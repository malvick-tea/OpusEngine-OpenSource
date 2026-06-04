namespace Opus.Engine.Physics.Ground;

/// <summary>Interpolated engine-speed-to-torque map.</summary>
public sealed class TorqueCurve
{
    private readonly TorquePoint[] _points;

    public TorqueCurve(IEnumerable<TorquePoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        _points = points.OrderBy(point => point.Rpm).ToArray();
        if (_points.Length == 0)
        {
            throw new ArgumentException("At least one torque point is required.", nameof(points));
        }

        ValidatePoints();
    }

    public float TorqueNewtonMetersAt(float rpm)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rpm);
        if (rpm <= _points[0].Rpm)
        {
            return _points[0].TorqueNewtonMeters;
        }

        for (var i = 1; i < _points.Length; i++)
        {
            if (rpm <= _points[i].Rpm)
            {
                return Interpolate(_points[i - 1], _points[i], rpm);
            }
        }

        return _points[^1].TorqueNewtonMeters;
    }

    public static TorqueCurve BroadPeak(float idleRpm, float peakRpm, float redlineRpm, float peakTorqueNewtonMeters)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(peakTorqueNewtonMeters);
        return new TorqueCurve(new[]
        {
            new TorquePoint(idleRpm, peakTorqueNewtonMeters * 0.72f),
            new TorquePoint(peakRpm, peakTorqueNewtonMeters),
            new TorquePoint(redlineRpm, peakTorqueNewtonMeters * 0.68f),
        });
    }

    private void ValidatePoints()
    {
        for (var i = 0; i < _points.Length; i++)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(_points[i].Rpm);
            ArgumentOutOfRangeException.ThrowIfNegative(_points[i].TorqueNewtonMeters);
            if (i > 0 && _points[i - 1].Rpm == _points[i].Rpm)
            {
                throw new ArgumentException("Torque-curve RPM values must be unique.");
            }
        }
    }

    private static float Interpolate(TorquePoint left, TorquePoint right, float rpm)
    {
        var t = (rpm - left.Rpm) / (right.Rpm - left.Rpm);
        return left.TorqueNewtonMeters + ((right.TorqueNewtonMeters - left.TorqueNewtonMeters) * t);
    }
}

/// <summary>One calibrated sample in an engine torque map.</summary>
public readonly record struct TorquePoint(float Rpm, float TorqueNewtonMeters);
