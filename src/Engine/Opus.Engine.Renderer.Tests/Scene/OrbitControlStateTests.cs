using System;
using FluentAssertions;
using Opus.Engine.Renderer.Scene;
using Xunit;

namespace Opus.Engine.Renderer.Tests.Scene;

public sealed class OrbitControlStateTests
{
    private const float BaseSpeed = 0.35f;
    private const float HalfRevolutionSeconds = MathF.PI * 2f / BaseSpeed / 2f;

    [Fact]
    public void New_state_starts_unpaused_at_default_speed_and_zero_phase()
    {
        var state = new OrbitControlState(BaseSpeed);

        state.Phase.Should().Be(0f);
        state.IsPaused.Should().BeFalse();
        state.SpeedMultiplier.Should().Be(OrbitControlState.DefaultSpeedMultiplier);
        state.BaseRadiansPerSecond.Should().Be(BaseSpeed);
        state.EffectiveRadiansPerSecond.Should().Be(BaseSpeed);
    }

    [Fact]
    public void Ctor_rejects_non_positive_or_non_finite_base_speed()
    {
        var negative = () => new OrbitControlState(-1f);
        var zero = () => new OrbitControlState(0f);
        var nan = () => new OrbitControlState(float.NaN);
        var inf = () => new OrbitControlState(float.PositiveInfinity);

        negative.Should().Throw<ArgumentOutOfRangeException>();
        zero.Should().Throw<ArgumentOutOfRangeException>();
        nan.Should().Throw<ArgumentOutOfRangeException>();
        inf.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Tick_advances_phase_proportionally_to_delta_and_speed()
    {
        var state = new OrbitControlState(BaseSpeed);

        state.Tick(HalfRevolutionSeconds);

        state.Phase.Should().BeApproximately(0.5f, 1e-5f);
    }

    [Fact]
    public void Tick_wraps_phase_to_unit_interval()
    {
        var state = new OrbitControlState(BaseSpeed);

        state.Tick(HalfRevolutionSeconds * 2f + HalfRevolutionSeconds * 0.5f);

        state.Phase.Should().BeInRange(0f, 1f);
        state.Phase.Should().BeApproximately(0.25f, 1e-5f);
    }

    [Fact]
    public void Tick_ignores_non_positive_and_non_finite_delta()
    {
        var state = new OrbitControlState(BaseSpeed);

        state.Tick(0f);
        state.Tick(-1f);
        state.Tick(float.NaN);
        state.Tick(float.PositiveInfinity);

        state.Phase.Should().Be(0f);
    }

    [Fact]
    public void Tick_is_frozen_while_paused()
    {
        var state = new OrbitControlState(BaseSpeed);
        state.TogglePause();

        state.Tick(HalfRevolutionSeconds);

        state.IsPaused.Should().BeTrue();
        state.Phase.Should().Be(0f);
        state.EffectiveRadiansPerSecond.Should().Be(0f);
    }

    [Fact]
    public void Pause_toggle_resumes_advance_after_second_press()
    {
        var state = new OrbitControlState(BaseSpeed);

        state.TogglePause();
        state.TogglePause();
        state.Tick(HalfRevolutionSeconds);

        state.IsPaused.Should().BeFalse();
        state.Phase.Should().BeApproximately(0.5f, 1e-5f);
    }

    [Fact]
    public void IncreaseSpeed_multiplies_by_step_factor()
    {
        var state = new OrbitControlState(BaseSpeed);

        state.IncreaseSpeed();

        state.SpeedMultiplier.Should().BeApproximately(
            OrbitControlState.DefaultSpeedMultiplier * OrbitControlState.SpeedStepFactor,
            1e-5f);
    }

    [Fact]
    public void IncreaseSpeed_clamps_to_max()
    {
        var state = new OrbitControlState(BaseSpeed);

        for (var i = 0; i < 20; i++)
        {
            state.IncreaseSpeed();
        }

        state.SpeedMultiplier.Should().Be(OrbitControlState.MaxSpeedMultiplier);
    }

    [Fact]
    public void DecreaseSpeed_divides_by_step_factor()
    {
        var state = new OrbitControlState(BaseSpeed);

        state.DecreaseSpeed();

        state.SpeedMultiplier.Should().BeApproximately(
            OrbitControlState.DefaultSpeedMultiplier / OrbitControlState.SpeedStepFactor,
            1e-5f);
    }

    [Fact]
    public void DecreaseSpeed_clamps_to_min()
    {
        var state = new OrbitControlState(BaseSpeed);

        for (var i = 0; i < 20; i++)
        {
            state.DecreaseSpeed();
        }

        state.SpeedMultiplier.Should().Be(OrbitControlState.MinSpeedMultiplier);
    }

    [Fact]
    public void ResetSpeed_snaps_back_to_default_from_either_direction()
    {
        var fast = new OrbitControlState(BaseSpeed);
        fast.IncreaseSpeed();
        fast.IncreaseSpeed();
        fast.IncreaseSpeed();

        var slow = new OrbitControlState(BaseSpeed);
        slow.DecreaseSpeed();
        slow.DecreaseSpeed();
        slow.DecreaseSpeed();

        fast.ResetSpeed();
        slow.ResetSpeed();

        fast.SpeedMultiplier.Should().Be(OrbitControlState.DefaultSpeedMultiplier);
        slow.SpeedMultiplier.Should().Be(OrbitControlState.DefaultSpeedMultiplier);
    }

    [Fact]
    public void Speed_multiplier_scales_effective_speed()
    {
        var state = new OrbitControlState(BaseSpeed);
        state.IncreaseSpeed();

        state.EffectiveRadiansPerSecond.Should().BeApproximately(
            BaseSpeed * OrbitControlState.SpeedStepFactor,
            1e-5f);
    }

    [Fact]
    public void Tick_under_double_speed_doubles_phase_progress()
    {
        var fast = new OrbitControlState(BaseSpeed);
        fast.IncreaseSpeed();
        fast.IncreaseSpeed();

        var baseline = new OrbitControlState(BaseSpeed);

        var quarter = HalfRevolutionSeconds / 2f;
        fast.Tick(quarter);
        baseline.Tick(quarter);

        fast.Phase.Should().BeGreaterThan(baseline.Phase);
        var ratio = fast.Phase / baseline.Phase;
        ratio.Should().BeApproximately(OrbitControlState.SpeedStepFactor * OrbitControlState.SpeedStepFactor, 1e-4f);
    }

    [Fact]
    public void AdvancePhase_adds_delta_to_phase_and_wraps_to_unit()
    {
        var state = new OrbitControlState(BaseSpeed);

        state.AdvancePhase(0.25f);
        state.AdvancePhase(0.5f);

        state.Phase.Should().BeApproximately(0.75f, 1e-5f);
    }

    [Fact]
    public void AdvancePhase_with_negative_delta_walks_backwards()
    {
        var state = new OrbitControlState(BaseSpeed);
        state.AdvancePhase(0.3f);

        state.AdvancePhase(-0.1f);

        state.Phase.Should().BeApproximately(0.2f, 1e-5f);
    }

    [Fact]
    public void AdvancePhase_wraps_across_zero()
    {
        var state = new OrbitControlState(BaseSpeed);

        state.AdvancePhase(-0.25f);

        state.Phase.Should().BeApproximately(0.75f, 1e-5f);
    }

    [Fact]
    public void AdvancePhase_ignores_non_finite_input()
    {
        var state = new OrbitControlState(BaseSpeed);
        state.AdvancePhase(0.4f);
        var before = state.Phase;

        state.AdvancePhase(float.NaN);
        state.AdvancePhase(float.PositiveInfinity);

        state.Phase.Should().Be(before);
    }

    [Fact]
    public void AdvancePhase_works_while_paused()
    {
        var state = new OrbitControlState(BaseSpeed);
        state.TogglePause();

        state.AdvancePhase(0.3f);

        state.Phase.Should().BeApproximately(0.3f, 1e-5f);
    }
}
