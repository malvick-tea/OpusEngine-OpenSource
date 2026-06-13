using System;
using Opus.Engine.AlphaStress.FramePacing;

namespace Opus.Engine.Host.Windows.Direct3D12.Frame;

/// <summary>
/// Configuration record for the production alpha-frame budget watchdog. Disabled by
/// default; consumers opt in through <see cref="D3D12OpusApplicationOptions"/> when
/// they want the host to emit warnings each time the rolling frame window breaches
/// the configured thresholds. Closes the M5.1 "alpha frame budget observed but not
/// enforced" lead follow-up; the M11 thresholds power the evaluation.
/// </summary>
/// <param name="Enabled">When true the host evaluates the rolling frame window on
/// every <see cref="EvaluationInterval"/> tick and emits a warning on breach.</param>
/// <param name="EvaluationInterval">Wall-clock cadence at which the watchdog evaluates
/// the rolling frame window. Defaults to once per second so the host log stays
/// readable instead of one line per frame.</param>
/// <param name="Thresholds">Per-window thresholds the watchdog evaluates the rolling
/// pacing summary against. Null falls back to <see cref="FramePacingThresholds.Default"/>
/// which mirrors the M5.1 <c>AlphaFrameBudget</c>.</param>
public sealed record D3D12AlphaFrameBudgetPolicy(
    bool Enabled,
    TimeSpan EvaluationInterval,
    FramePacingThresholds? Thresholds)
{
    /// <summary>Default cadence — once per second — sized so a tester run produces
    /// one log line per failing window instead of one per frame.</summary>
    public static readonly TimeSpan DefaultEvaluationInterval = TimeSpan.FromSeconds(1);

    /// <summary>Disabled-by-default policy. Production hosts opt in explicitly.</summary>
    public static D3D12AlphaFrameBudgetPolicy Disabled { get; } = new(
        Enabled: false,
        EvaluationInterval: DefaultEvaluationInterval,
        Thresholds: null);

    /// <summary>Enabled policy with default cadence + default thresholds. Convenience
    /// constructor for consumers that want the canonical alpha-frame contract enforced
    /// without authoring their own threshold record.</summary>
    public static D3D12AlphaFrameBudgetPolicy Enable() => new(
        Enabled: true,
        EvaluationInterval: DefaultEvaluationInterval,
        Thresholds: null);

    /// <summary>Returns the threshold record the watchdog should use. Resolves the
    /// nullable field to <see cref="FramePacingThresholds.Default"/> when unset.</summary>
    public FramePacingThresholds EffectiveThresholds => Thresholds ?? FramePacingThresholds.Default;

    /// <summary>Throws when the policy is internally inconsistent.</summary>
    public void Validate()
    {
        if (EvaluationInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(EvaluationInterval),
                "EvaluationInterval must be positive.");
        }

        EffectiveThresholds.Validate();
    }
}
