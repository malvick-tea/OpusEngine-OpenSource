namespace Opus.Engine.Physics.Ground;

/// <summary>Power source, gearing, and driven-radius properties for a ground vehicle.</summary>
public sealed record PowertrainProperties
{
    private readonly float[] _forwardGearRatios;

    public PowertrainProperties(
        TorqueCurve torqueCurve,
        IEnumerable<float> forwardGearRatios,
        float reverseGearRatio,
        float finalDriveRatio,
        float drivenRadiusMeters,
        float efficiency,
        float idleRpm,
        float maximumPowerWatts,
        float upshiftRpm,
        float downshiftRpm)
    {
        ArgumentNullException.ThrowIfNull(torqueCurve);
        ArgumentNullException.ThrowIfNull(forwardGearRatios);
        _forwardGearRatios = forwardGearRatios.ToArray();
        ValidateRatios(_forwardGearRatios, nameof(forwardGearRatios));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(reverseGearRatio);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(finalDriveRatio);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(drivenRadiusMeters);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(efficiency);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(efficiency, 1f);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(idleRpm);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPowerWatts);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(upshiftRpm, idleRpm);
        ArgumentOutOfRangeException.ThrowIfNegative(downshiftRpm);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(downshiftRpm, upshiftRpm);

        TorqueCurve = torqueCurve;
        ReverseGearRatio = reverseGearRatio;
        FinalDriveRatio = finalDriveRatio;
        DrivenRadiusMeters = drivenRadiusMeters;
        Efficiency = efficiency;
        IdleRpm = idleRpm;
        MaximumPowerWatts = maximumPowerWatts;
        UpshiftRpm = upshiftRpm;
        DownshiftRpm = downshiftRpm;
    }

    public TorqueCurve TorqueCurve { get; }

    public IReadOnlyList<float> ForwardGearRatios => _forwardGearRatios;

    public float ReverseGearRatio { get; }

    public float FinalDriveRatio { get; }

    public float DrivenRadiusMeters { get; }

    public float Efficiency { get; }

    public float IdleRpm { get; }

    public float MaximumPowerWatts { get; }

    public float UpshiftRpm { get; }

    public float DownshiftRpm { get; }

    private static void ValidateRatios(IReadOnlyList<float> ratios, string argumentName)
    {
        if (ratios.Count == 0)
        {
            throw new ArgumentException("At least one forward gear ratio is required.", argumentName);
        }

        for (var i = 0; i < ratios.Count; i++)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ratios[i], argumentName);
        }
    }
}
