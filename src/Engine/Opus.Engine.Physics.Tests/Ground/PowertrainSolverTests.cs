using FluentAssertions;
using Opus.Engine.Physics.Ground;
using Xunit;

namespace Opus.Engine.Physics.Tests.Ground;

public sealed class PowertrainSolverTests
{
    [Fact]
    public void Torque_curve_interpolates_between_samples()
    {
        var curve = new TorqueCurve(new[]
        {
            new TorquePoint(1000f, 100f),
            new TorquePoint(2000f, 200f),
        });

        curve.TorqueNewtonMetersAt(1500f).Should().BeApproximately(150f, 0.001f);
    }

    [Fact]
    public void Forward_solver_upshifts_above_threshold()
    {
        var output = PowertrainSolver.Evaluate(Powertrain(), signedSpeedMps: 12f, throttle: 1f, forwardGearIndex: 0);

        output.ForwardGearIndex.Should().Be(1);
        output.DrivenForceNewtons.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void Reverse_throttle_produces_negative_force()
    {
        var output = PowertrainSolver.Evaluate(Powertrain(), signedSpeedMps: 0f, throttle: -1f, forwardGearIndex: 0);

        output.DrivenForceNewtons.Should().BeLessThan(0f);
    }

    internal static PowertrainProperties Powertrain(float peakTorque = 900f) => new(
        TorqueCurve.BroadPeak(600f, 1600f, 3000f, peakTorque),
        new[] { 5f, 2.5f, 1.2f },
        reverseGearRatio: 4f,
        finalDriveRatio: 3f,
        drivenRadiusMeters: 0.4f,
        efficiency: 0.8f,
        idleRpm: 600f,
        maximumPowerWatts: 150000f,
        upshiftRpm: 2600f,
        downshiftRpm: 1000f);
}
