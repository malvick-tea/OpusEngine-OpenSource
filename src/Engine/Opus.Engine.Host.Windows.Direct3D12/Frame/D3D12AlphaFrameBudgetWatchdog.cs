using System;
using System.Globalization;
using Opus.Engine.AlphaStress.FramePacing;
using Opus.Foundation;

namespace Opus.Engine.Host.Windows.Direct3D12.Frame;

/// <summary>
/// Rolling watchdog that folds per-frame CPU times into a sliding window, evaluates the
/// window against <see cref="FramePacingThresholds"/> on every cadence tick, and emits a
/// single structured warning line when the rolling p95 / p99 / max / hitch counter
/// breaches its limit. Owned by <see cref="D3D12OpusApplication"/>; not thread-safe by
/// design — the host advances the watchdog from the render thread.
/// </summary>
public sealed class D3D12AlphaFrameBudgetWatchdog
{
    private readonly D3D12AlphaFrameBudgetPolicy _policy;
    private readonly ILog _log;
    private readonly TimeProvider _time;
    private FramePacingAggregator _aggregator;
    private long _frameCounter;
    private DateTimeOffset _windowStartedAtUtc;
    private int _breachCount;

    /// <summary>Creates a watchdog. The supplied <paramref name="policy"/> must be
    /// enabled; callers should not allocate this type for a disabled policy.</summary>
    public D3D12AlphaFrameBudgetWatchdog(D3D12AlphaFrameBudgetPolicy policy, ILog log, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(log);
        policy.Validate();
        if (!policy.Enabled)
        {
            throw new ArgumentException(
                "D3D12AlphaFrameBudgetWatchdog requires an enabled policy.",
                nameof(policy));
        }

        _policy = policy;
        _log = log;
        _time = time ?? TimeProvider.System;
        _aggregator = new FramePacingAggregator(policy.EffectiveThresholds.HitchThreshold);
        _windowStartedAtUtc = _time.GetUtcNow();
    }

    /// <summary>Number of evaluation windows that breached the configured thresholds
    /// since the watchdog was created.</summary>
    public int BreachCount => _breachCount;

    /// <summary>Folds a per-frame CPU time into the rolling window. When the elapsed
    /// time since the last evaluation crosses <see cref="D3D12AlphaFrameBudgetPolicy.EvaluationInterval"/>
    /// the watchdog evaluates the window against the thresholds and resets the
    /// aggregator. Non-positive times are silently ignored — the host may pass through
    /// zero-frame ticks during pause / catch-up handling.</summary>
    public void RecordFrame(TimeSpan cpuFrameTime)
    {
        if (cpuFrameTime <= TimeSpan.Zero)
        {
            return;
        }

        _frameCounter++;
        _aggregator.Record(new FramePacingObservation(
            FrameNumber: _frameCounter,
            ObservedAtUtc: _time.GetUtcNow(),
            CpuFrameTime: cpuFrameTime));
        var now = _time.GetUtcNow();
        if (now - _windowStartedAtUtc < _policy.EvaluationInterval)
        {
            return;
        }

        EvaluateWindow(now);
    }

    private void EvaluateWindow(DateTimeOffset evaluatedAtUtc)
    {
        var summary = _aggregator.BuildSummary();
        if (summary.HasSamples && BreachesThresholds(summary))
        {
            _breachCount++;
            _log.Warn(FormatBreach(summary, evaluatedAtUtc));
        }

        _aggregator = new FramePacingAggregator(_policy.EffectiveThresholds.HitchThreshold);
        _windowStartedAtUtc = evaluatedAtUtc;
    }

    private bool BreachesThresholds(FramePacingSummary summary)
    {
        var thresholds = _policy.EffectiveThresholds;
        return summary.Percentile95 > thresholds.P95Limit
            || summary.Percentile99 > thresholds.P99Limit
            || summary.Max > thresholds.MaxLimit
            || summary.HitchCount > thresholds.HitchCountLimit;
    }

    private string FormatBreach(FramePacingSummary summary, DateTimeOffset evaluatedAtUtc)
    {
        var thresholds = _policy.EffectiveThresholds;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"alpha frame budget breach: window of {summary.SampleCount} frames ending {evaluatedAtUtc:O} — p95={summary.Percentile95.TotalMilliseconds:F2}ms (limit {thresholds.P95Limit.TotalMilliseconds:F2}ms), p99={summary.Percentile99.TotalMilliseconds:F2}ms, max={summary.Max.TotalMilliseconds:F2}ms, hitches={summary.HitchCount} (limit {thresholds.HitchCountLimit})");
    }
}
