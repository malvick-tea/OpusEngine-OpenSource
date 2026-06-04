namespace Opus.Engine.Physics.Ground;

/// <summary>Maps road speed and throttle to driven force through automatic gearing.</summary>
public static class PowertrainSolver
{
    private const float MinimumPowerLimitedSpeedMps = 0.5f;

    public static PowertrainOutput Evaluate(
        PowertrainProperties powertrain,
        float signedSpeedMps,
        float throttle,
        int forwardGearIndex)
    {
        ArgumentNullException.ThrowIfNull(powertrain);
        var clampedThrottle = Math.Clamp(throttle, -1f, 1f);
        if (clampedThrottle < 0f)
        {
            return EvaluateReverse(powertrain, signedSpeedMps, clampedThrottle);
        }

        var gear = SelectForwardGear(powertrain, signedSpeedMps, forwardGearIndex);
        var ratio = powertrain.ForwardGearRatios[gear];
        var rpm = DrivenRpm(powertrain, signedSpeedMps, ratio);
        var force = WheelForce(powertrain, rpm, ratio, clampedThrottle);
        return new PowertrainOutput(force, rpm, gear);
    }

    private static PowertrainOutput EvaluateReverse(
        PowertrainProperties powertrain,
        float signedSpeedMps,
        float throttle)
    {
        var rpm = DrivenRpm(powertrain, signedSpeedMps, powertrain.ReverseGearRatio);
        var force = WheelForce(powertrain, rpm, powertrain.ReverseGearRatio, throttle);
        return new PowertrainOutput(force, rpm, 0);
    }

    private static int SelectForwardGear(PowertrainProperties powertrain, float speedMps, int currentGear)
    {
        var gear = Math.Clamp(currentGear, 0, powertrain.ForwardGearRatios.Count - 1);
        var rpm = DrivenRpm(powertrain, speedMps, powertrain.ForwardGearRatios[gear]);
        if (rpm > powertrain.UpshiftRpm && gear < powertrain.ForwardGearRatios.Count - 1)
        {
            return gear + 1;
        }

        if (rpm < powertrain.DownshiftRpm && gear > 0)
        {
            return gear - 1;
        }

        return gear;
    }

    private static float DrivenRpm(PowertrainProperties powertrain, float speedMps, float gearRatio)
    {
        var wheelRpm = MathF.Abs(speedMps) * 60f / (2f * MathF.PI * powertrain.DrivenRadiusMeters);
        return MathF.Max(powertrain.IdleRpm, wheelRpm * gearRatio * powertrain.FinalDriveRatio);
    }

    private static float WheelForce(PowertrainProperties powertrain, float rpm, float ratio, float throttle)
    {
        var torqueForce = powertrain.TorqueCurve.TorqueNewtonMetersAt(rpm)
            * ratio * powertrain.FinalDriveRatio * powertrain.Efficiency
            / powertrain.DrivenRadiusMeters;
        var angularSpeed = rpm * (2f * MathF.PI / 60f);
        var speedForPowerLimit = MathF.Max(
            MinimumPowerLimitedSpeedMps,
            angularSpeed * powertrain.DrivenRadiusMeters / (ratio * powertrain.FinalDriveRatio));
        var powerForce = powertrain.MaximumPowerWatts * powertrain.Efficiency / speedForPowerLimit;
        return MathF.Min(torqueForce, powerForce) * throttle;
    }
}

/// <summary>Powertrain state derived for one integration substep.</summary>
public readonly record struct PowertrainOutput(float DrivenForceNewtons, float EngineRpm, int ForwardGearIndex);
