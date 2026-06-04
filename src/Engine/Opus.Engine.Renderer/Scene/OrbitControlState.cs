using System;

namespace Opus.Engine.Renderer.Scene;

/// <summary>
/// Backend-agnostic state machine driving an orbit-camera screen (Garage, model viewers,
/// future scene-preview tools). Owns the temporal slice — phase, pause flag, speed
/// multiplier — that a renderer-side <c>OrbitCamera</c> reads each frame. Pure: no GPU
/// dependencies, no event subscriptions, fully unit-testable.
/// </summary>
/// <remarks>
/// Phase is normalised to <c>[0, 1)</c> — one full revolution per unit. Speed multiplier
/// snaps multiplicatively per step (factor <see cref="SpeedStepFactor"/>) and clamps to
/// <c>[<see cref="MinSpeedMultiplier"/>, <see cref="MaxSpeedMultiplier"/>]</c>. Pause is a
/// boolean gate that freezes phase advancement without disturbing the multiplier — resume
/// keeps the previous speed setting.
/// </remarks>
public sealed class OrbitControlState
{
    /// <summary>Lowest speed multiplier reachable via repeated <see cref="DecreaseSpeed"/>.</summary>
    public const float MinSpeedMultiplier = 0.25f;

    /// <summary>Highest speed multiplier reachable via repeated <see cref="IncreaseSpeed"/>.</summary>
    public const float MaxSpeedMultiplier = 4.0f;

    /// <summary>Default multiplier — also the target of <see cref="ResetSpeed"/>.</summary>
    public const float DefaultSpeedMultiplier = 1.0f;

    /// <summary>Multiplicative step per <see cref="IncreaseSpeed"/> / inverse per
    /// <see cref="DecreaseSpeed"/>. Six presses span the full <c>0.25×</c> ↔ <c>4×</c>
    /// envelope.</summary>
    public const float SpeedStepFactor = 1.5f;

    private const float Epsilon = 1e-5f;

    private readonly float _baseRadiansPerSecond;
    private float _phase;
    private float _speedMultiplier = DefaultSpeedMultiplier;

    /// <summary>Constructs a state-machine spinning at <paramref name="baseRadiansPerSecond"/>
    /// when the multiplier is 1.0. Must be strictly positive.</summary>
    public OrbitControlState(float baseRadiansPerSecond)
    {
        if (!(baseRadiansPerSecond > 0f) || float.IsNaN(baseRadiansPerSecond) || float.IsInfinity(baseRadiansPerSecond))
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseRadiansPerSecond),
                baseRadiansPerSecond,
                "Base orbit speed must be a finite positive value.");
        }

        _baseRadiansPerSecond = baseRadiansPerSecond;
    }

    public float Phase => _phase;

    public bool IsPaused { get; private set; }

    public float SpeedMultiplier => _speedMultiplier;

    public float BaseRadiansPerSecond => _baseRadiansPerSecond;

    public float EffectiveRadiansPerSecond => IsPaused ? 0f : _baseRadiansPerSecond * _speedMultiplier;

    /// <summary>Advances the phase by <paramref name="deltaSeconds"/> at the current
    /// effective speed. No-op when paused or when delta is non-positive / non-finite.</summary>
    public void Tick(float deltaSeconds)
    {
        if (IsPaused || !(deltaSeconds > 0f) || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds))
        {
            return;
        }

        var revolutionsPerSecond = _baseRadiansPerSecond * _speedMultiplier / (MathF.PI * 2f);
        _phase = WrapUnit(_phase + deltaSeconds * revolutionsPerSecond);
    }

    public void TogglePause() => IsPaused = !IsPaused;

    /// <summary>Manually advances the phase by <paramref name="deltaPhaseFraction"/> units —
    /// one unit equals one full revolution. Used by drag-to-rotate input on top of (or
    /// instead of, while paused) the auto-spin. Wraps to <c>[0, 1)</c>. Non-finite deltas
    /// are dropped.</summary>
    public void AdvancePhase(float deltaPhaseFraction)
    {
        if (float.IsNaN(deltaPhaseFraction) || float.IsInfinity(deltaPhaseFraction))
        {
            return;
        }

        _phase = WrapUnit(_phase + deltaPhaseFraction);
    }

    public void IncreaseSpeed() =>
        _speedMultiplier = Math.Min(MaxSpeedMultiplier, _speedMultiplier * SpeedStepFactor);

    public void DecreaseSpeed() =>
        _speedMultiplier = Math.Max(MinSpeedMultiplier, _speedMultiplier / SpeedStepFactor);

    public void ResetSpeed() => _speedMultiplier = DefaultSpeedMultiplier;

    private static float WrapUnit(float phase)
    {
        var wrapped = phase - MathF.Floor(phase);
        return wrapped >= 1f - Epsilon ? 0f : wrapped;
    }
}
